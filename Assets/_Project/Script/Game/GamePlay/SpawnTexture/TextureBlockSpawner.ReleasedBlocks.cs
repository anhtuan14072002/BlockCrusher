using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using Collider = Unity.Physics.Collider;
using PhysicsMaterial = Unity.Physics.Material;
using RenderMaterial = UnityEngine.Material;

public sealed partial class TextureBlockSpawner
{
    private void QueueReleasedBlockSpawn(Vector3 localPosition, Color32 color, ushort typeIndex, Vector3 sawCenter,
        Vector3 pressDirection,
        float pressSpeed, float outwardForce, float tangentialForce, float spinDirection, float bladeRadius,
        float sideDamping, float maxVelocity)
    {
        if (!EnsureReleasedBlockResources() || !EnsureEcsReady() || typeIndex >= _releasedBlockTypes.Count)
            return;
        ReleasedBlockRuntimeType variantGroup = _releasedBlockTypes[typeIndex];
        int cellX = Mathf.RoundToInt((localPosition.x - _offset.x) / _cellSize);
        int cellY = Mathf.RoundToInt((localPosition.y - _offset.y) / _cellSize);
        uint seed = math.hash(new int3(cellX, cellY,
            _debrisSpawnSequence++ + variantGroup.FirstVariantIndex * 397));
        int variantOffset = (int)(seed % variantGroup.VariantCount);
        typeIndex = (ushort)(variantGroup.FirstVariantIndex + variantOffset);
        float scaleMultiplier = Mathf.Lerp(0.72f, 1.28f, Hash01(seed ^ 0x9e3779b9u));
        float rotationRadians = Mathf.Lerp(-Mathf.PI, Mathf.PI, Hash01(seed ^ 0x85ebca6bu));
        float angularSpeed = Mathf.Lerp(-6f, 6f, Hash01(seed ^ 0xc2b2ae35u));
        Vector3 position = _runtimeParent.TransformPoint(localPosition);
        Vector3 outward = position - sawCenter;
        outward.z = 0f;
        float distance = outward.magnitude;
        if (distance <= 0.0001f)
            outward = pressDirection.sqrMagnitude > 0.0001f ? pressDirection : Vector3.up;
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
        CreateReleasedBlockEntity(position, color, typeIndex, clampedVelocity, safeMaxVelocity, false, true,
            scaleMultiplier, rotationRadians, angularSpeed);
    }
    private Entity CreateReleasedBlockEntity(Vector3 position, Color32 color, ushort typeIndex, Vector3 velocity,
        float maxVelocity, bool renderAsMetaball, bool enableSolidConstraint, float scaleMultiplier,
        float rotationRadians, float angularSpeed)
    {
        ReleasedBlockRuntimeType runtimeType = _releasedBlockTypes[typeIndex];
        if (_releasedBlockEntities.Count >= _maxReleasedPhysicsBlocks)
            TrimReleasedBlockEntityList();
        while (_releasedBlockEntities.Count >= _maxReleasedPhysicsBlocks && _releasedBlockEntities.Count > 0)
            DestroyReleasedBlockEntityAt(0);

        Entity entity = _entityManager.CreateEntity(_releasedBlockArchetype);
        _entityManager.SetComponentData(entity, LocalTransform.FromPositionRotationScale(
            new float3(position.x, position.y, position.z), quaternion.RotateZ(rotationRadians),
            runtimeType.Scale * scaleMultiplier));
        _entityManager.SetComponentData(entity, new PhysicsCollider { Value = runtimeType.Collider });
        PhysicsMass physicsMass = PhysicsMass.CreateDynamic(
            runtimeType.Collider.Value.MassProperties, _releasedBlockMass);
        physicsMass.InverseInertia.x = 0f;
        physicsMass.InverseInertia.y = 0f;
        _entityManager.SetComponentData(entity, physicsMass);
        _entityManager.SetComponentData(entity, new PhysicsVelocity
        {
            Linear = new float3(velocity.x, velocity.y, velocity.z),
            Angular = new float3(0f, 0f, angularSpeed)
        });
        _entityManager.SetComponentData(entity, new PhysicsDamping
        {
            Linear = _releasedBlockDamping,
            Angular = _releasedBlockAngularDamping
        });
        _entityManager.SetComponentData(entity, new PhysicsGravityFactor { Value = 1f });
        _entityManager.SetComponentData(entity, new ReleasedBlockComponent
        {
            OwnerId = _ownerId,
            Color = ToFloat4(color),
            LockedZ = position.z,
            MaxPlanarSpeed = maxVelocity,
            Radius = runtimeType.Radius * scaleMultiplier,
            TypeIndex = typeIndex,
            CollectibleId = renderAsMetaball ? default : runtimeType.CollectibleId,
            SuctionPathIndex = byte.MaxValue,
            SolidConstraintFrames = enableSolidConstraint ? ReleasedBlockComponent.SolidConstraintDuration : (byte)0,
            RenderAsMetaball = renderAsMetaball ? (byte)1 : (byte)0
        });
        _entityManager.SetComponentEnabled<ReleasedBlockSolidConstraint>(entity, enableSolidConstraint);
        _entityManager.SetComponentEnabled<SuctionTransit>(entity, false);
        _releasedBlockEntities.Add(entity);
        return entity;
    }
    private static float Hash01(uint value)
    {
        value ^= value >> 16;
        value *= 0x7feb352du;
        value ^= value >> 15;
        value *= 0x846ca68bu;
        value ^= value >> 16;
        return (value & 0x00ffffffu) / 16777215f;
    }
    private float GetSafePhysicsVelocity(float requestedMaxVelocity)
    {
        float fixedDeltaTime = Mathf.Max(Time.fixedDeltaTime, MinimumPhysicsDeltaTime);
        float maxCellTravelVelocity = _cellSize * MaxCellTravelPerStep / fixedDeltaTime;
        return Mathf.Min(requestedMaxVelocity, maxCellTravelVelocity);
    }
    private Vector3 RedirectVelocityFromSolid(Vector3 worldPosition, Vector3 velocity)
    {
        velocity.z = 0f;
        float speed = velocity.magnitude;
        if (speed <= 0.0001f)
            return velocity;
        Vector3 direction = velocity / speed;
        float nearProbeDistance = _cellSize * 0.9f;
        float farProbeDistance = _cellSize * 1.6f;
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
        Vector3 lateralOffset = new Vector3(-direction.y, direction.x, 0f) * (_cellSize * 0.45f);
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
