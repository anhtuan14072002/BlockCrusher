using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public sealed partial class LevelMapSpawner
{
    internal static void NotifyItemSucked(string collectibleId, int count)
    {
        SuckedItemCounts.TryGetValue(collectibleId, out int currentCount);
        SuckedItemCounts[collectibleId] = currentCount + count;
        SuckedBlockCount += count;
        ItemSucked?.Invoke(collectibleId, count);
    }
    internal static int ActiveSpawnerCount => ActiveSpawners.Count;
    internal static LevelMapSpawner GetActiveSpawner(int index) => ActiveSpawners[index];
    internal static void RemoveActiveSpawnerAt(int index) => ActiveSpawners.RemoveAt(index);
    public static bool ReleaseInBoxForActiveSpawners(Matrix4x4 cutLocalToWorld, Bounds cutLocalBounds,
        Vector3 pressDirection, float pressSpeed, float outwardForce, float tangentialForce, float spinDirection,
        float bladeRadius, float sideDamping, float maxVelocity)
    {
        bool releasedAny = false;
        for (int i = ActiveSpawners.Count - 1; i >= 0; i--)
        {
            LevelMapSpawner spawner = ActiveSpawners[i];
            if (spawner == null)
            {
                ActiveSpawners.RemoveAt(i);
                continue;
            }
            releasedAny |= spawner.ReleaseInBox(cutLocalToWorld, cutLocalBounds, pressDirection, pressSpeed,
                outwardForce, tangentialForce, spinDirection, bladeRadius, sideDamping, maxVelocity);
        }
        return releasedAny;
    }
    public static void ApplyConveyorForActiveSpawners(Bounds bounds, Vector3 direction, float speed, float acceleration,
        float deltaTime)
    {
        ReleasedBlockInteractionQueue.Enqueue(new ReleasedBlockInteractionRequest
        {
            Type = ReleasedBlockInteractionType.Conveyor,
            BoundsMin = ToFloat3(bounds.min),
            BoundsMax = ToFloat3(bounds.max),
            Direction = ToFloat3(direction),
            Speed = speed,
            Acceleration = acceleration,
            DeltaTime = deltaTime
        });
    }
    public static void ClearReleasedBlocksForActiveSpawners(Bounds bounds)
    {
        ReleasedBlockInteractionQueue.Enqueue(new ReleasedBlockInteractionRequest
        {
            Type = ReleasedBlockInteractionType.Clear,
            BoundsMin = ToFloat3(bounds.min),
            BoundsMax = ToFloat3(bounds.max)
        });
    }
    public static void ApplySuctionForActiveSpawners(Vector3 origin, Quaternion rotation, Vector3 boxSize,
        float force, float acceleration, float maxVelocity, float arrivalDamping, float destroyRadius,
        float waypointRadius, float pathLookAhead, float renderDepth, FixedList512Bytes<float3> suctionPath,
        float deltaTime, bool allowCapture)
    {
        Quaternion inverseRotation = Quaternion.Inverse(rotation);
        ReleasedBlockInteractionQueue.Enqueue(new ReleasedBlockInteractionRequest
        {
            Type = ReleasedBlockInteractionType.Suction,
            Origin = ToFloat3(origin),
            InverseRotation = new quaternion(inverseRotation.x, inverseRotation.y, inverseRotation.z,
                inverseRotation.w),
            HalfSize = ToFloat3(boxSize * 0.5f),
            BoxOffset = ToFloat3(rotation * new Vector3(boxSize.x * 0.5f, 0f, 0f)),
            Force = force,
            Acceleration = acceleration,
            MaxVelocity = maxVelocity,
            ArrivalDamping = arrivalDamping,
            DestroyRadius = destroyRadius,
            WaypointRadius = waypointRadius,
            PathLookAhead = pathLookAhead,
            RenderDepth = renderDepth,
            SuctionPath = suctionPath,
            DeltaTime = deltaTime,
            AllowSuctionCapture = allowCapture ? (byte)1 : (byte)0
        });
    }
}
