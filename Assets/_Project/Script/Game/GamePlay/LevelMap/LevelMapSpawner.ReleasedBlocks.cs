using Crusher;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

public sealed partial class LevelMapSpawner
{
    internal void SpawnBreakableFragments(BreakableObstacle obstacle, Vector3 sawDirection)
    {
        if (!EnsureReleasedBlockResources() || !EnsureEcsReady())
            return;

        Mesh sourceMesh = obstacle.GetComponent<MeshFilter>().sharedMesh;
        Bounds bounds = sourceMesh.bounds;
        Vector3 center = obstacle.transform.TransformPoint(bounds.center);
        Vector3 pushDirection = sawDirection;
        pushDirection.z = 0f;
        pushDirection = pushDirection.sqrMagnitude > 0.0001f ? pushDirection.normalized : Vector3.down;
        float maxVelocity = GetSafePhysicsVelocity(Mathf.Max(1f, obstacle.BreakForce * 2f));
        var random = new System.Random(obstacle.GetInstanceID());

        for (int i = 0; i < obstacle.ShardCount; i++)
        {
            Vector3 localPosition = new(
                Mathf.Lerp(bounds.min.x, bounds.max.x, (float)random.NextDouble()),
                Mathf.Lerp(bounds.min.y, bounds.max.y, (float)random.NextDouble()),
                Mathf.Lerp(bounds.min.z, bounds.max.z, (float)random.NextDouble()));
            Vector3 position = obstacle.transform.TransformPoint(localPosition);
            Vector3 outward = position - center;
            outward.z = 0f;
            outward = outward.sqrMagnitude > 0.0001f ? outward.normalized : Vector3.up;
            Vector3 velocity = pushDirection * (obstacle.BreakForce * 0.55f) +
                               outward * (obstacle.BreakForce * 0.35f) + Vector3.up * 0.4f;
            velocity.y = Mathf.Max(0f, velocity.y);
            velocity = Vector3.ClampMagnitude(velocity, maxVelocity);
            float angularVelocity = ((float)random.NextDouble() * 2f - 1f) * 10f;
            CreateReleasedBlockEntity(position, obstacle.ReleasedColor, obstacle.GetReleasedTypeIndex(i),
                velocity, maxVelocity, false, true, angularVelocity, true);
        }
    }

    private void QueueReleasedBlockSpawn(Vector3 localPosition, Color32 color, ushort typeIndex, Vector3 sawCenter,
        float pressSpeed, float outwardForce, float tangentialForce, float spinDirection, float bladeRadius,
        float sideDamping, float maxVelocity)
    {
        if (!EnsureReleasedBlockResources() || !EnsureEcsReady() || typeIndex >= _releasedBlockTypes.Count)
            return;
        Vector3 position = _runtimeParent.TransformPoint(localPosition);
        Vector3 outward = position - sawCenter;
        outward.z = 0f;
        float distance = outward.magnitude;
        outward.y = Mathf.Max(0f, outward.y);
        if (outward.sqrMagnitude <= 0.0001f)
            outward = Vector3.up;
        else
            outward.Normalize();
        float radiusPush = bladeRadius > 0f ? Mathf.Clamp01((bladeRadius - distance) / bladeRadius) : 0f;
        Vector3 tangent = new Vector3(-outward.y, outward.x, 0f) * Mathf.Sign(spinDirection);
        float speedScale = 1f + Mathf.Min(pressSpeed, 4f) * 0.1f;
        float pushScale = 1f + radiusPush * 1.25f;
        Vector3 velocity = outward * (outwardForce * 0.08f * pushScale * speedScale) +
                           (Vector3.up + tangent * 0.1f).normalized *
                           (tangentialForce * 0.21f * pushScale * speedScale * radiusPush);
        velocity -= outward * Vector3.Dot(velocity, outward) * sideDamping * radiusPush * 0.15f;
        float safeMaxVelocity = GetSafePhysicsVelocity(maxVelocity);
        Vector3 clampedVelocity = Vector3.ClampMagnitude(velocity, safeMaxVelocity);
        clampedVelocity = RedirectVelocityFromSolid(position, clampedVelocity);
        clampedVelocity = ApplyReleaseLift(position, clampedVelocity, safeMaxVelocity);
        CreateReleasedBlockEntity(position, color, typeIndex, clampedVelocity, safeMaxVelocity, false, true);
    }
    private Entity CreateReleasedBlockEntity(Vector3 position, Color32 color, ushort typeIndex, Vector3 velocity,
        float maxVelocity, bool renderAsMetaball, bool enableSolidConstraint, float angularVelocity = 0f,
        bool usesGravity = true)
    {
        ReleasedBlockRuntimeType runtimeType = _releasedBlockTypes[typeIndex];
        if (!renderAsMetaball)
            position = ProjectToReleasedBlockPlane(position);
        maxVelocity = GetSafePhysicsVelocity(maxVelocity, runtimeType.Radius);
        velocity = Vector3.ClampMagnitude(velocity, maxVelocity);
        if (_releasedBlockEntities.Count >= _maxReleasedPhysicsBlocks)
            TrimReleasedBlockEntityList();
        while (_releasedBlockEntities.Count >= _maxReleasedPhysicsBlocks && _releasedBlockEntities.Count > 0)
            DestroyReleasedBlockEntityAt(0);

        Entity entity = _entityManager.CreateEntity(_releasedBlockArchetype);
        _entityManager.SetComponentData(entity, LocalTransform.FromPositionRotationScale(
            new float3(position.x, position.y, position.z), quaternion.identity, runtimeType.Scale));
        _entityManager.SetComponentData(entity, new PhysicsCollider { Value = runtimeType.Collider });
        PhysicsMass physicsMass = PhysicsMass.CreateDynamic(
            runtimeType.Collider.Value.MassProperties, _releasedBlockMass);
        physicsMass.InverseInertia.x = 0f;
        physicsMass.InverseInertia.y = 0f;
        _entityManager.SetComponentData(entity, physicsMass);
        _entityManager.SetComponentData(entity, new PhysicsVelocity
        {
            Linear = new float3(velocity.x, velocity.y, velocity.z),
            Angular = new float3(0f, 0f, angularVelocity)
        });
        _entityManager.SetComponentData(entity, new PhysicsDamping
        {
            Linear = _releasedBlockDamping,
            Angular = _releasedBlockAngularDamping
        });
        _entityManager.SetComponentData(entity, new PhysicsGravityFactor { Value = renderAsMetaball ? 0f : 1f });
        _entityManager.SetComponentData(entity, new ReleasedBlockComponent
        {
            OwnerId = _ownerId,
            Color = ToFloat4(color),
            LockedZ = position.z,
            MaxPlanarSpeed = maxVelocity,
            Radius = runtimeType.Radius,
            PhysicsStepStartPosition = new float3(position.x, position.y, position.z),
            TypeIndex = typeIndex,
            CollectibleType = renderAsMetaball ? TypeBlock.Water : runtimeType.CollectibleType,
            SuctionPathIndex = byte.MaxValue,
            RenderAsMetaball = renderAsMetaball ? (byte)1 : (byte)0,
            UsesGravity = renderAsMetaball || !usesGravity ? (byte)0 : (byte)1
        });
        _entityManager.SetComponentEnabled<ReleasedBlockSolidConstraint>(entity, enableSolidConstraint);
        _entityManager.SetComponentEnabled<SuctionTransit>(entity, false);
        _releasedBlockEntities.Add(entity);
        return entity;
    }
    private Vector3 ProjectToReleasedBlockPlane(Vector3 worldPosition)
    {
        if (_runtimeParent == null)
            return worldPosition;

        Vector3 localPosition = _runtimeParent.InverseTransformPoint(worldPosition);
        localPosition.z = 0f;
        return _runtimeParent.TransformPoint(localPosition);
    }
    private float GetSafePhysicsVelocity(float requestedMaxVelocity)
    {
        float fixedDeltaTime = Mathf.Max(Time.fixedDeltaTime, MinimumPhysicsDeltaTime);
        float maxCellTravelVelocity = GetWorldCellSize() * MaxCellTravelPerStep / fixedDeltaTime;
        return Mathf.Min(requestedMaxVelocity, maxCellTravelVelocity);
    }
    private static float GetSafePhysicsVelocity(float requestedMaxVelocity, float worldRadius)
    {
        float fixedDeltaTime = Mathf.Max(Time.fixedDeltaTime, MinimumPhysicsDeltaTime);
        float maxRadiusTravelVelocity = worldRadius * MaxRadiusTravelPerStep / fixedDeltaTime;
        return Mathf.Min(requestedMaxVelocity, maxRadiusTravelVelocity);
    }
    private Vector3 ApplyReleaseLift(Vector3 worldPosition, Vector3 velocity, float maxVelocity)
    {
        velocity.y = Mathf.Max(0f, velocity.y);
        float worldCellSize = GetWorldCellSize();
        float nearProbeDistance = worldCellSize * 0.9f;
        float farProbeDistance = worldCellSize * 1.6f;
        if (_releasedBlockLiftSpeed > 0f &&
            !IsSolidAlongDirection(worldPosition, Vector3.up, nearProbeDistance, farProbeDistance))
        {
            velocity.y = Mathf.Max(velocity.y, _releasedBlockLiftSpeed);
        }
        return Vector3.ClampMagnitude(velocity, maxVelocity);
    }
    private Vector3 RedirectVelocityFromSolid(Vector3 worldPosition, Vector3 velocity)
    {
        velocity.z = 0f;
        float speed = velocity.magnitude;
        if (speed <= 0.0001f)
            return velocity;
        Vector3 direction = velocity / speed;
        float worldCellSize = GetWorldCellSize();
        float nearProbeDistance = worldCellSize * 0.9f;
        float farProbeDistance = worldCellSize * 1.6f;
        if (!IsSolidAlongDirection(worldPosition, direction, nearProbeDistance, farProbeDistance))
            return velocity;
        Vector3 tangent = new Vector3(-direction.y, direction.x, 0f);
        Vector3 bestDirection = Vector3.zero;
        float bestAlignment = float.MinValue;
        EvaluateFreeDirection(worldPosition, tangent, direction, nearProbeDistance, farProbeDistance,
            ref bestDirection, ref bestAlignment);
        EvaluateFreeDirection(worldPosition, -tangent, direction, nearProbeDistance, farProbeDistance,
            ref bestDirection, ref bestAlignment);
        EvaluateFreeDirection(worldPosition, Vector3.up, direction, nearProbeDistance, farProbeDistance,
            ref bestDirection, ref bestAlignment);
        EvaluateFreeDirection(worldPosition, Vector3.down, direction, nearProbeDistance, farProbeDistance,
            ref bestDirection, ref bestAlignment);
        EvaluateFreeDirection(worldPosition, Vector3.left, direction, nearProbeDistance, farProbeDistance,
            ref bestDirection, ref bestAlignment);
        EvaluateFreeDirection(worldPosition, Vector3.right, direction, nearProbeDistance, farProbeDistance,
            ref bestDirection, ref bestAlignment);
        return bestDirection.sqrMagnitude > 0f ? bestDirection * speed : Vector3.zero;
    }
    private void EvaluateFreeDirection(Vector3 worldPosition, Vector3 candidate, Vector3 desired,
        float nearProbeDistance, float farProbeDistance, ref Vector3 bestDirection, ref float bestAlignment)
    {
        candidate.z = 0f;
        candidate.Normalize();
        if (IsSolidAlongDirection(worldPosition, candidate, nearProbeDistance, farProbeDistance))
            return;
        float alignment = Vector3.Dot(candidate, desired);
        if (alignment <= bestAlignment)
            return;
        bestAlignment = alignment;
        bestDirection = candidate;
    }
    private bool IsSolidAlongDirection(Vector3 worldPosition, Vector3 direction,
        float nearProbeDistance, float farProbeDistance)
    {
        Vector3 lateralOffset = new Vector3(-direction.y, direction.x, 0f) * (GetWorldCellSize() * 0.45f);
        Vector3 nearPoint = worldPosition + direction * nearProbeDistance;
        Vector3 farPoint = worldPosition + direction * farProbeDistance;
        return IsSolidAtWorldCell(nearPoint) ||
               IsSolidAtWorldCell(farPoint) ||
               IsSolidAtWorldCell(nearPoint + lateralOffset) ||
               IsSolidAtWorldCell(nearPoint - lateralOffset) ||
               IsSolidAtWorldCell(farPoint + lateralOffset) ||
               IsSolidAtWorldCell(farPoint - lateralOffset);
    }
    private bool IsSolidAtWorldCell(Vector3 worldPosition)
    {
        if (!_cellSolid.IsCreated || _runtimeParent == null)
            return false;
        Vector3 local = _runtimeParent.InverseTransformPoint(worldPosition);
        int x = Mathf.RoundToInt((local.x - _offset.x) / _cellSize);
        int y = Mathf.RoundToInt((local.y - _offset.y) / _cellSize);
        if ((uint)x >= (uint)_gridWidth || (uint)y >= (uint)_gridHeight)
            return false;
        return _cellSolid[y * _gridWidth + x] != 0;
    }
}
