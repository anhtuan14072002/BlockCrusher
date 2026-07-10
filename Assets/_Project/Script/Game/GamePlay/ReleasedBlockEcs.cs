using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

public struct TextureBlockSpawnerData : IComponentData
{
    public int GridWidth;
    public int GridHeight;
    public float CellSize;
    public float3 Offset;
    public float4x4 WorldToLocal;
    public float4x4 LocalToWorld;
    public float Gravity;
    public float Damping;
    public float Restitution;
    public float Friction;
    public float3 SawPosition;
    public float SawRadius;
    public float SawForce;
}

public struct TextureBlockCell : IBufferElementData
{
    public byte Solid;
}

public struct ReleasedBlockData : IComponentData
{
    public Entity Owner;
    public float3 Position;
    public float3 Velocity;
    public float4 Color;
}

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[BurstCompile]
public partial struct ReleasedBlockSimulationSystem : ISystem
{
    private EntityQuery _releasedBlocks;

    public void OnCreate(ref SystemState state)
    {
        _releasedBlocks = state.GetEntityQuery(ComponentType.ReadWrite<ReleasedBlockData>());
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (_releasedBlocks.IsEmptyIgnoreFilter)
            return;

        float deltaTime = SystemAPI.Time.DeltaTime;
        BufferLookup<TextureBlockCell> cells = SystemAPI.GetBufferLookup<TextureBlockCell>(true);
        ComponentLookup<TextureBlockSpawnerData> spawners = SystemAPI.GetComponentLookup<TextureBlockSpawnerData>(true);
        NativeArray<Entity> entities = _releasedBlocks.ToEntityArray(Allocator.Temp);
        NativeArray<ReleasedBlockData> blocks = _releasedBlocks.ToComponentDataArray<ReleasedBlockData>(Allocator.Temp);

        for (int i = 0; i < blocks.Length; i++)
        {
            ReleasedBlockData blockData = blocks[i];
            Entity owner = blockData.Owner;
            if (!spawners.HasComponent(owner) || !cells.HasBuffer(owner))
                continue;

            TextureBlockSpawnerData spawner = spawners[owner];
            DynamicBuffer<TextureBlockCell> cellBuffer = cells[owner];
            StepBlock(ref blockData, spawner, cellBuffer, deltaTime);
            blocks[i] = blockData;
        }

        ResolveBlockPairs(blocks, spawners);

        for (int i = 0; i < blocks.Length; i++)
            state.EntityManager.SetComponentData(entities[i], blocks[i]);

        blocks.Dispose();
        entities.Dispose();
    }

    private static void StepBlock(ref ReleasedBlockData block, TextureBlockSpawnerData spawner,
        DynamicBuffer<TextureBlockCell> cells, float deltaTime)
    {
        float3 previousPosition = block.Position;
        float3 velocity = block.Velocity;
        velocity.y -= spawner.Gravity * deltaTime;

        float3 sawDelta = previousPosition - spawner.SawPosition;
        sawDelta.z = 0f;
        float sawDistance = math.length(sawDelta);
        if (sawDistance > 0.0001f && sawDistance < spawner.SawRadius)
        {
            velocity += sawDelta * ((spawner.SawRadius - sawDistance) /
                                    (spawner.SawRadius * sawDistance) * spawner.SawForce * deltaTime);
        }

        velocity *= 1f / (1f + spawner.Damping * deltaTime);
        float3 position = previousPosition + velocity * deltaTime;

        if (ResolveGridCollisionSwept(previousPosition, ref position, spawner, cells, out float3 collisionNormal))
        {
            velocity = math.reflect(velocity, collisionNormal) * spawner.Restitution;
            float3 tangentVelocity = velocity - collisionNormal * math.dot(velocity, collisionNormal);
            velocity -= tangentVelocity * spawner.Friction;
        }

        block.Position = position;
        block.Velocity = velocity;
    }

    private static void ResolveBlockPairs(NativeArray<ReleasedBlockData> blocks,
        ComponentLookup<TextureBlockSpawnerData> spawners)
    {
        for (int i = 0; i < blocks.Length - 1; i++)
        {
            ReleasedBlockData a = blocks[i];
            if (!spawners.HasComponent(a.Owner))
                continue;

            TextureBlockSpawnerData spawner = spawners[a.Owner];
            float minDistance = spawner.CellSize;
            float minDistanceSqr = minDistance * minDistance;

            for (int j = i + 1; j < blocks.Length; j++)
            {
                ReleasedBlockData b = blocks[j];
                if (b.Owner != a.Owner)
                    continue;

                float3 delta = b.Position - a.Position;
                delta.z = 0f;
                float distanceSqr = math.lengthsq(delta);
                if (distanceSqr >= minDistanceSqr)
                    continue;

                float distance = math.sqrt(distanceSqr);
                float3 normal = distance > 0.0001f ? delta / distance : PairFallbackNormal(i, j);
                float penetration = minDistance - distance;
                float3 correction = normal * (penetration * 0.5f);

                a.Position -= correction;
                b.Position += correction;

                float normalSpeed = math.dot(b.Velocity - a.Velocity, normal);
                if (normalSpeed < 0f)
                {
                    float impulse = -(1f + spawner.Restitution) * normalSpeed * 0.5f;
                    float3 impulseVector = normal * impulse;
                    a.Velocity -= impulseVector;
                    b.Velocity += impulseVector;

                    float3 relativeVelocity = b.Velocity - a.Velocity;
                    float3 tangentVelocity = relativeVelocity - normal * math.dot(relativeVelocity, normal);
                    float3 frictionVector = tangentVelocity * (spawner.Friction * 0.5f);
                    a.Velocity += frictionVector;
                    b.Velocity -= frictionVector;
                }

                blocks[i] = a;
                blocks[j] = b;
            }
        }
    }

    private static float3 PairFallbackNormal(int a, int b)
    {
        uint hash = (uint)(a * 73856093) ^ (uint)(b * 19349663);
        float angle = (hash & 1023u) * (6.28318530718f / 1023f);
        return new float3(math.cos(angle), math.sin(angle), 0f);
    }

    private static bool ResolveGridCollisionSwept(float3 previousWorldPosition, ref float3 worldPosition,
        TextureBlockSpawnerData spawner, DynamicBuffer<TextureBlockCell> cells, out float3 collisionNormal)
    {
        collisionNormal = 0f;
        float3 fromLocal = math.transform(spawner.WorldToLocal, previousWorldPosition);
        float3 toLocal = math.transform(spawner.WorldToLocal, worldPosition);
        float2 delta = toLocal.xy - fromLocal.xy;
        float distance = math.length(delta);
        int steps = math.clamp((int)math.ceil(distance / math.max(spawner.CellSize * 0.35f, 0.0001f)), 1, 12);

        for (int i = 1; i <= steps; i++)
        {
            float t = i / (float)steps;
            float3 sampleWorldPosition = math.lerp(previousWorldPosition, worldPosition, t);

            if (!ResolveGridCollision(ref sampleWorldPosition, spawner, cells, out collisionNormal))
                continue;

            worldPosition = sampleWorldPosition;
            return true;
        }

        return false;
    }

    private static bool ResolveGridCollision(ref float3 worldPosition, TextureBlockSpawnerData spawner,
        DynamicBuffer<TextureBlockCell> cells, out float3 collisionNormal)
    {
        collisionNormal = 0f;
        float3 localPosition = math.transform(spawner.WorldToLocal, worldPosition);
        float parentScaleX = math.max(math.length(new float3(spawner.LocalToWorld.c0.x, spawner.LocalToWorld.c0.y,
            spawner.LocalToWorld.c0.z)), 0.0001f);
        float parentScaleY = math.max(math.length(new float3(spawner.LocalToWorld.c1.x, spawner.LocalToWorld.c1.y,
            spawner.LocalToWorld.c1.z)), 0.0001f);
        float halfWidthLocal = spawner.CellSize * 0.5f / parentScaleX;
        float halfHeightLocal = spawner.CellSize * 0.5f / parentScaleY;
        float cellHalf = spawner.CellSize * 0.5f;
        int centerX = (int)math.round((localPosition.x - spawner.Offset.x) / spawner.CellSize);
        int centerY = (int)math.round((localPosition.y - spawner.Offset.y) / spawner.CellSize);
        int radiusX = (int)math.ceil((halfWidthLocal + cellHalf) / spawner.CellSize) + 1;
        int radiusY = (int)math.ceil((halfHeightLocal + cellHalf) / spawner.CellSize) + 1;
        int minX = math.max(0, centerX - radiusX);
        int maxX = math.min(spawner.GridWidth - 1, centerX + radiusX);
        int minY = math.max(0, centerY - radiusY);
        int maxY = math.min(spawner.GridHeight - 1, centerY + radiusY);
        bool resolved = false;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                int cellIndex = y * spawner.GridWidth + x;
                if (cells[cellIndex].Solid == 0)
                    continue;

                float3 cellLocal = spawner.Offset + new float3(x * spawner.CellSize, y * spawner.CellSize, 0f);
                float deltaX = localPosition.x - cellLocal.x;
                float deltaY = localPosition.y - cellLocal.y;
                float overlapX = halfWidthLocal + cellHalf - math.abs(deltaX);
                float overlapY = halfHeightLocal + cellHalf - math.abs(deltaY);
                if (overlapX <= 0f || overlapY <= 0f)
                    continue;

                float3 localNormal;
                if (overlapX < overlapY)
                {
                    float sign = deltaX >= 0f ? 1f : -1f;
                    localPosition.x += overlapX * sign;
                    localNormal = new float3(sign, 0f, 0f);
                }
                else
                {
                    float sign = deltaY >= 0f ? 1f : -1f;
                    localPosition.y += overlapY * sign;
                    localNormal = new float3(0f, sign, 0f);
                }

                collisionNormal = math.normalizesafe(TransformVector(spawner.LocalToWorld, localNormal));
                resolved = true;
            }
        }

        if (resolved)
            worldPosition = math.transform(spawner.LocalToWorld, localPosition);

        return resolved;
    }

    private static float3 TransformVector(float4x4 matrix, float3 vector)
    {
        return new float3(
            matrix.c0.x * vector.x + matrix.c1.x * vector.y + matrix.c2.x * vector.z,
            matrix.c0.y * vector.x + matrix.c1.y * vector.y + matrix.c2.y * vector.z,
            matrix.c0.z * vector.x + matrix.c1.z * vector.y + matrix.c2.z * vector.z);
    }
}
