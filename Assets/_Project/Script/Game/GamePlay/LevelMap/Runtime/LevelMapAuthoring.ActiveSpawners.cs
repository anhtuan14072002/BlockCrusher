using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;
using PhysicsCollider = Unity.Physics.Collider;

public sealed partial class LevelMapAuthoring
{
    private const float ToolCollisionSkin = 0.0002f;

    internal static World GetOrCreateDefaultWorld()
    {
        World world = World.DefaultGameObjectInjectionWorld;
        return world != null && world.IsCreated
            ? world
            : DefaultWorldInitialization.Initialize("Default World", false);
    }

    internal static int ActiveSpawnerCount => ActiveSpawners.Count;
    internal static LevelMapAuthoring GetActiveSpawner(int index) => ActiveSpawners[index];
    internal static void RemoveActiveSpawnerAt(int index) => ActiveSpawners.RemoveAt(index);
    public static bool ReleaseInBoxForActiveSpawners(Matrix4x4 cutLocalToWorld, Bounds cutLocalBounds,
        Vector3[] cutVertices, int[] cutTriangles, float maxVelocity)
    {
        bool releasedAny = false;
        for (int i = ActiveSpawners.Count - 1; i >= 0; i--)
        {
            LevelMapAuthoring spawner = ActiveSpawners[i];
            if (spawner == null)
            {
                ActiveSpawners.RemoveAt(i);
                continue;
            }
            releasedAny |= spawner.ReleaseInBox(
                cutLocalToWorld, cutLocalBounds, cutVertices, cutTriangles, maxVelocity);
        }
        return releasedAny;
    }
    internal static Vector3 ClampToolTarget(Vector3 from, Vector3 to,
        BlobAssetReference<PhysicsCollider> toolCollider, Quaternion toolRotation,
        Vector3 bodyCenterOffset, Vector2 bodyHalfSize, Quaternion bodyOrientation)
    {
        to = ClampToolTargetToSolidCells(
            from, to, toolRotation, bodyCenterOffset, bodyHalfSize, bodyOrientation);
        Vector3 position = from;
        Vector3 remaining = to - from;
        remaining.z = 0f;

        // A second cast lets the head slide along a wall instead of sticking on first contact.
        for (int iteration = 0; iteration < 2; iteration++)
        {
            if (remaining.sqrMagnitude <= 0.00000001f)
                break;

            Vector3 target = position + remaining;
            if (!TryCastTool(position, target, toolCollider, toolRotation, out ColliderCastHit hit))
                return target;

            Vector3 normal = new Vector3(hit.SurfaceNormal.x, hit.SurfaceNormal.y, hit.SurfaceNormal.z).normalized;
            normal.z = 0f;
            if (normal.sqrMagnitude <= 0.00000001f)
                return position;
            normal.Normalize();

            position = Vector3.LerpUnclamped(position, target, hit.Fraction) + normal * ToolCollisionSkin;
            remaining = target - position;
            float intoSurface = Vector3.Dot(remaining, normal);
            if (intoSurface < 0f)
                remaining -= normal * intoSurface;
        }

        position.z = from.z;
        return position;
    }

    private static bool TryCastTool(Vector3 from, Vector3 to,
        BlobAssetReference<PhysicsCollider> toolCollider, Quaternion toolRotation,
        out ColliderCastHit closestHit)
    {
        ColliderCastInput input = new ColliderCastInput(
            toolCollider, ToFloat3(from), ToFloat3(to),
            new quaternion(toolRotation.x, toolRotation.y, toolRotation.z, toolRotation.w));
        closestHit = default;
        float closestFraction = 1f;
        bool hasHit = LevelObstacle.TryCastTool(input, ref closestHit, ref closestFraction);
        return hasHit;
    }

    private static Vector3 ClampToolTargetToSolidCells(Vector3 from, Vector3 to, Quaternion rotation,
        Vector3 centerOffset, Vector2 halfSize, Quaternion bodyOrientation)
    {
        Vector3 position = from;
        Vector3 remaining = to - from;
        remaining.z = 0f;
        float sampleStep = GetToolCollisionSampleStep(Mathf.Min(halfSize.x, halfSize.y));

        for (int iteration = 0; iteration < 2; iteration++)
        {
            // Rotation follows input independently. If the newly rotated PhysicsShape overlaps the
            // terrain, move the tool out first, then preserve only the tangent part of this frame's input.
            for (int depenetrationIteration = 0; depenetrationIteration < 8; depenetrationIteration++)
            {
                if (!TryGetToolSolidContact(
                        position, rotation, centerOffset, halfSize, bodyOrientation,
                        out Vector3 overlapNormal, out float overlapPenetration))
                    break;

                overlapNormal.z = 0f;
                if (overlapNormal.sqrMagnitude <= 0.00000001f)
                    break;

                overlapNormal.Normalize();
                position += overlapNormal * (overlapPenetration + ToolCollisionSkin);
                float overlapIntoSurface = Vector3.Dot(remaining, overlapNormal);
                if (overlapIntoSurface < 0f)
                    remaining -= overlapNormal * overlapIntoSurface;
            }

            float distance = remaining.magnitude;
            if (distance <= 0.000001f)
                break;

            Vector3 target = position + remaining;

            int steps = Mathf.Clamp(Mathf.CeilToInt(distance / sampleStep), 1, 128);
            Vector3 lastFree = position;
            bool hitSolid = false;
            Vector3 hitNormal = Vector3.zero;

            for (int step = 1; step <= steps; step++)
            {
                Vector3 sample = Vector3.Lerp(position, target, step / (float)steps);
                if (!TryGetToolSolidContact(sample, rotation, centerOffset, halfSize, bodyOrientation,
                        out hitNormal, out _))
                {
                    lastFree = sample;
                    continue;
                }

                Vector3 low = lastFree;
                Vector3 high = sample;
                for (int binaryIteration = 0; binaryIteration < 8; binaryIteration++)
                {
                    Vector3 middle = Vector3.Lerp(low, high, 0.5f);
                    if (TryGetToolSolidContact(middle, rotation, centerOffset, halfSize, bodyOrientation,
                            out hitNormal, out _))
                        high = middle;
                    else
                        low = middle;
                }

                position = low + hitNormal * ToolCollisionSkin;
                hitSolid = true;
                break;
            }

            if (!hitSolid)
                return target;

            remaining = target - position;
            float intoSurface = Vector3.Dot(remaining, hitNormal);
            if (intoSurface < 0f)
                remaining -= hitNormal * intoSurface;
        }

        return position;
    }

    private static float GetToolCollisionSampleStep(float minimumHalfSize)
    {
        float step = Mathf.Max(minimumHalfSize * 0.2f, 0.005f);
        for (int i = ActiveSpawners.Count - 1; i >= 0; i--)
        {
            LevelMapAuthoring spawner = ActiveSpawners[i];
            if (spawner == null || spawner._cellSize.x <= 0f || spawner._cellSize.y <= 0f)
                continue;

            float worldCellX = spawner._runtimeParent.TransformVector(
                Vector3.right * spawner._cellSize.x).magnitude;
            float worldCellY = spawner._runtimeParent.TransformVector(
                Vector3.up * spawner._cellSize.y).magnitude;
            step = Mathf.Min(step, Mathf.Max(Mathf.Min(worldCellX, worldCellY) * 0.2f, 0.005f));
        }

        return step;
    }

    private static bool TryGetToolSolidContact(Vector3 toolPosition, Quaternion rotation,
        Vector3 centerOffset, Vector2 halfSize, Quaternion bodyOrientation,
        out Vector3 bestNormal, out float bestPenetration)
    {
        bestNormal = Vector3.zero;
        bestPenetration = 0f;
        bool hasContact = false;
        Vector3 worldCenter = toolPosition + rotation * centerOffset;
        Quaternion worldOrientation = rotation * bodyOrientation;
        Vector3 worldHalfAxisX = worldOrientation * Vector3.right * halfSize.x;
        Vector3 worldHalfAxisY = worldOrientation * Vector3.up * halfSize.y;

        for (int spawnerIndex = ActiveSpawners.Count - 1; spawnerIndex >= 0; spawnerIndex--)
        {
            LevelMapAuthoring spawner = ActiveSpawners[spawnerIndex];
            if (spawner == null)
            {
                ActiveSpawners.RemoveAt(spawnerIndex);
                continue;
            }

            if (!spawner.TryGetSolidBoxContact(worldCenter, worldHalfAxisX, worldHalfAxisY,
                    out Vector3 normal, out float penetration) ||
                penetration <= bestPenetration)
                continue;

            bestNormal = normal;
            bestPenetration = penetration;
            hasContact = true;
        }

        return hasContact;
    }

    private bool TryGetSolidBoxContact(Vector3 worldCenter, Vector3 worldHalfAxisX, Vector3 worldHalfAxisY,
        out Vector3 worldNormal, out float worldPenetration)
    {
        worldNormal = Vector3.zero;
        worldPenetration = 0f;
        if (_cellSolidSnapshot == null || _runtimeParent == null ||
            _cellSize.x <= 0f || _cellSize.y <= 0f)
            return false;

        Vector3 localCenter3 = _runtimeParent.InverseTransformPoint(worldCenter);
        Vector2 localCenter = new Vector2(localCenter3.x, localCenter3.y);
        Vector3 localHalfAxisX3 = _runtimeParent.InverseTransformVector(worldHalfAxisX);
        Vector3 localHalfAxisY3 = _runtimeParent.InverseTransformVector(worldHalfAxisY);
        Vector2 localHalfAxisX = new Vector2(localHalfAxisX3.x, localHalfAxisX3.y);
        Vector2 localHalfAxisY = new Vector2(localHalfAxisY3.x, localHalfAxisY3.y);
        float boundRadius = localHalfAxisX.magnitude + localHalfAxisY.magnitude;
        int centerX = Mathf.RoundToInt((localCenter.x - _offset.x) / _cellSize.x);
        int centerY = Mathf.RoundToInt((localCenter.y - _offset.y) / _cellSize.y);
        int searchRadiusX = Mathf.CeilToInt(boundRadius / _cellSize.x + 0.5f);
        int searchRadiusY = Mathf.CeilToInt(boundRadius / _cellSize.y + 0.5f);
        Vector2 bestLocalNormal = Vector2.zero;
        float bestLocalPenetration = 0f;

        for (int y = centerY - searchRadiusY; y <= centerY + searchRadiusY; y++)
        {
            if ((uint)y >= (uint)_gridHeight)
                continue;

            for (int x = centerX - searchRadiusX; x <= centerX + searchRadiusX; x++)
            {
                int cellIndex = y * _gridWidth + x;
                if ((uint)x >= (uint)_gridWidth || _cellSolidSnapshot[cellIndex] == 0)
                    continue;

                Vector2 cellCenter = new Vector2(
                    _offset.x + x * _cellSize.x, _offset.y + y * _cellSize.y);
                if (!TryGetBoxCellContact(localCenter, localHalfAxisX, localHalfAxisY, cellCenter,
                        _cellSize * 0.5f, out Vector2 normal, out float penetration) ||
                    penetration <= bestLocalPenetration)
                    continue;
                bestLocalPenetration = penetration;
                bestLocalNormal = normal;
            }
        }

        if (bestLocalPenetration <= 0f)
            return false;

        worldNormal = _runtimeParent.TransformVector(
            new Vector3(bestLocalNormal.x, bestLocalNormal.y, 0f)).normalized;
        worldPenetration = _runtimeParent.TransformVector(
            new Vector3(bestLocalNormal.x, bestLocalNormal.y, 0f) * bestLocalPenetration).magnitude;
        return true;
    }

    private static bool TryGetBoxCellContact(Vector2 boxCenter, Vector2 halfAxisX, Vector2 halfAxisY,
        Vector2 cellCenter, Vector2 halfCell, out Vector2 contactNormal, out float penetration)
    {
        Vector2 centerDelta = boxCenter - cellCenter;
        contactNormal = Vector2.zero;
        penetration = float.MaxValue;

        Vector2 axisXNormal = halfAxisX.sqrMagnitude > 0.00000001f
            ? new Vector2(-halfAxisX.y, halfAxisX.x).normalized
            : Vector2.right;
        Vector2 axisYNormal = halfAxisY.sqrMagnitude > 0.00000001f
            ? new Vector2(-halfAxisY.y, halfAxisY.x).normalized
            : Vector2.up;

        if (!AccumulateBoxCellAxis(Vector2.right, centerDelta, halfAxisX, halfAxisY, halfCell,
                ref contactNormal, ref penetration) ||
            !AccumulateBoxCellAxis(Vector2.up, centerDelta, halfAxisX, halfAxisY, halfCell,
                ref contactNormal, ref penetration) ||
            !AccumulateBoxCellAxis(axisXNormal, centerDelta, halfAxisX, halfAxisY, halfCell,
                ref contactNormal, ref penetration) ||
            !AccumulateBoxCellAxis(axisYNormal, centerDelta, halfAxisX, halfAxisY, halfCell,
                ref contactNormal, ref penetration))
            return false;

        return penetration < float.MaxValue;
    }

    private static bool AccumulateBoxCellAxis(Vector2 axis, Vector2 centerDelta,
        Vector2 halfAxisX, Vector2 halfAxisY, Vector2 halfCell,
        ref Vector2 contactNormal, ref float penetration)
    {
        float boxRadius = Mathf.Abs(Vector2.Dot(halfAxisX, axis)) +
                          Mathf.Abs(Vector2.Dot(halfAxisY, axis));
        float cellRadius = halfCell.x * Mathf.Abs(axis.x) + halfCell.y * Mathf.Abs(axis.y);
        float signedDistance = Vector2.Dot(centerDelta, axis);
        float overlap = boxRadius + cellRadius - Mathf.Abs(signedDistance);
        if (overlap <= 0f)
            return false;

        if (overlap < penetration)
        {
            penetration = overlap;
            contactNormal = signedDistance >= 0f ? axis : -axis;
        }

        return true;
    }
}
