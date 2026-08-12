using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

public sealed partial class LevelMapAuthoring
{
    internal void ReleaseResourcesUnderObstacle(BreakableObstacle obstacle)
    {
        if (!_breakableCellMask.IsCreated || !_cellReleasedTypes.IsCreated || _runtimeParent == null)
            return;

        LevelObstacle levelObstacle = obstacle.GetComponent<LevelObstacle>();
        if (levelObstacle == null)
            return;

        CompleteReleasedBlockJobs();
        Vector3 obstacleCenter = obstacle.transform.position;
        for (int y = 0; y < _gridHeight; y++)
        {
            for (int x = 0; x < _gridWidth; x++)
            {
                int cellIndex = y * _gridWidth + x;
                if (_breakableCellMask[cellIndex] == 0)
                    continue;

                Vector3 cellLocal = GetCellLocalPosition(x, y);
                if (!levelObstacle.ContainsWorldPoint(_runtimeParent.TransformPoint(cellLocal)))
                    continue;

                _breakableCellMask[cellIndex] = 0;
                ReleaseCell(cellIndex, x, y, cellLocal, obstacleCenter, 0f);
            }
        }
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
        _entityManager.SetComponentData(entity, new PhysicsGravityFactor { Value = usesGravity ? 1f : 0f });
        _entityManager.SetComponentData(entity, new ReleasedBlockComponent
        {
            OwnerId = _ownerId,
            Color = ToFloat4(color),
            LockedZ = position.z,
            MaxPlanarSpeed = maxVelocity,
            Radius = runtimeType.Radius,
            PhysicsStepStartPosition = new float3(position.x, position.y, position.z),
            TypeIndex = typeIndex,
            SuctionPathIndex = byte.MaxValue,
            RenderAsMetaball = renderAsMetaball ? (byte)1 : (byte)0,
            UsesGravity = usesGravity ? (byte)1 : (byte)0
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
}
