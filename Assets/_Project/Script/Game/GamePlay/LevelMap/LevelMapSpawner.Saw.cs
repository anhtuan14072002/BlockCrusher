using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public sealed partial class LevelMapSpawner
{
    public bool ReleaseInBox(Matrix4x4 cutLocalToWorld, Bounds cutLocalBounds,
        Vector3[] cutVertices, int[] cutTriangles, Vector3 pressDirection,
        float pressSpeed, float outwardForce, float tangentialForce, float spinDirection, float bladeRadius,
        float sideDamping, float maxVelocity)
    {
        if (!_cellSolid.IsCreated || _runtimeParent == null)
            return false;

        Vector3 sawCenter = cutLocalToWorld.MultiplyPoint3x4(cutLocalBounds.center);
        QueueSawPush(sawCenter, pressDirection, pressSpeed, outwardForce, tangentialForce, spinDirection,
            bladeRadius, maxVelocity);

        Matrix4x4 cutWorldToLocal = cutLocalToWorld.inverse;
        Vector3 boundsMin = cutLocalBounds.min;
        Vector3 boundsMax = cutLocalBounds.max;
        Vector2 halfCell = _cellSize * 0.5f;
        Vector3 cellAxisX = cutWorldToLocal.MultiplyVector(
            _runtimeParent.TransformVector(Vector3.right * halfCell.x));
        Vector3 cellAxisY = cutWorldToLocal.MultiplyVector(
            _runtimeParent.TransformVector(Vector3.up * halfCell.y));
        Vector3 cellCutExtents = new(
            Mathf.Abs(cellAxisX.x) + Mathf.Abs(cellAxisY.x),
            Mathf.Abs(cellAxisX.y) + Mathf.Abs(cellAxisY.y),
            0f);
        Vector3 overlapMin = boundsMin - cellCutExtents;
        Vector3 overlapMax = boundsMax + cellCutExtents;
        Vector3 min = new(float.PositiveInfinity, float.PositiveInfinity);
        Vector3 max = new(float.NegativeInfinity, float.NegativeInfinity);
        for (int i = 0; i < 4; i++)
        {
            Vector3 corner = new(
                (i & 1) == 0 ? boundsMin.x : boundsMax.x,
                (i & 2) == 0 ? boundsMin.y : boundsMax.y,
                cutLocalBounds.center.z);
            Vector3 gridLocalCorner = _runtimeParent.InverseTransformPoint(cutLocalToWorld.MultiplyPoint3x4(corner));
            min = Vector3.Min(min, gridLocalCorner);
            max = Vector3.Max(max, gridLocalCorner);
        }

        int minX = Mathf.Max(0, Mathf.FloorToInt((min.x - _offset.x) / _cellSize.x));
        int maxX = Mathf.Min(_gridWidth - 1, Mathf.CeilToInt((max.x - _offset.x) / _cellSize.x));
        int minY = Mathf.Max(0, Mathf.FloorToInt((min.y - _offset.y) / _cellSize.y));
        int maxY = Mathf.Min(_gridHeight - 1, Mathf.CeilToInt((max.y - _offset.y) / _cellSize.y));
        bool releasedAny = false;
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                int cellIndex = y * _gridWidth + x;
                if (_cellSolid[cellIndex] == 0)
                    continue;

                Vector3 cellLocal = GetCellLocalPosition(x, y);
                Vector3 cutLocal = cutWorldToLocal.MultiplyPoint3x4(_runtimeParent.TransformPoint(cellLocal));
                if (cutLocal.x < overlapMin.x || cutLocal.x > overlapMax.x ||
                    cutLocal.y < overlapMin.y || cutLocal.y > overlapMax.y)
                    continue;

                if (!MeshOverlapsCell(cutVertices, cutTriangles, cutLocal, cellAxisX, cellAxisY))
                    continue;

                if (!releasedAny)
                    CompleteReleasedBlockJobs();
                ReleaseCell(cellIndex, x, y, cellLocal, sawCenter, pressSpeed, outwardForce, tangentialForce,
                    spinDirection, bladeRadius, sideDamping, maxVelocity);
                releasedAny = true;
            }
        }
        return releasedAny;
    }

    private static bool MeshOverlapsCell(Vector3[] vertices, int[] triangles, Vector3 center,
        Vector3 axisX, Vector3 axisY)
    {
        Vector2 a = center - axisX - axisY;
        Vector2 b = center + axisX - axisY;
        Vector2 c = center + axisX + axisY;
        Vector2 d = center - axisX + axisY;
        float minX = Mathf.Min(Mathf.Min(a.x, b.x), Mathf.Min(c.x, d.x));
        float maxX = Mathf.Max(Mathf.Max(a.x, b.x), Mathf.Max(c.x, d.x));
        float minY = Mathf.Min(Mathf.Min(a.y, b.y), Mathf.Min(c.y, d.y));
        float maxY = Mathf.Max(Mathf.Max(a.y, b.y), Mathf.Max(c.y, d.y));

        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector2 t0 = vertices[triangles[i]];
            Vector2 t1 = vertices[triangles[i + 1]];
            Vector2 t2 = vertices[triangles[i + 2]];
            if (Mathf.Abs(Cross(t1 - t0, t2 - t0)) < 0.000001f)
                continue;

            if (Mathf.Max(Mathf.Max(t0.x, t1.x), t2.x) < minX ||
                Mathf.Min(Mathf.Min(t0.x, t1.x), t2.x) > maxX ||
                Mathf.Max(Mathf.Max(t0.y, t1.y), t2.y) < minY ||
                Mathf.Min(Mathf.Min(t0.y, t1.y), t2.y) > maxY)
                continue;

            if (PointInTriangle(a, t0, t1, t2) || PointInTriangle(b, t0, t1, t2) ||
                PointInTriangle(c, t0, t1, t2) || PointInTriangle(d, t0, t1, t2) ||
                PointInQuad(t0, a, b, c, d) || PointInQuad(t1, a, b, c, d) ||
                PointInQuad(t2, a, b, c, d) ||
                EdgesIntersect(t0, t1, a, b, c, d) || EdgesIntersect(t1, t2, a, b, c, d) ||
                EdgesIntersect(t2, t0, a, b, c, d))
                return true;
        }

        return false;
    }

    private static bool PointInTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
    {
        float ab = Cross(b - a, point - a);
        float bc = Cross(c - b, point - b);
        float ca = Cross(a - c, point - c);
        return ab >= 0f && bc >= 0f && ca >= 0f || ab <= 0f && bc <= 0f && ca <= 0f;
    }

    private static bool PointInQuad(Vector2 point, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        return PointInTriangle(point, a, b, c) || PointInTriangle(point, a, c, d);
    }

    private static bool EdgesIntersect(Vector2 a, Vector2 b, Vector2 q0, Vector2 q1, Vector2 q2, Vector2 q3)
    {
        return SegmentsIntersect(a, b, q0, q1) || SegmentsIntersect(a, b, q1, q2) ||
               SegmentsIntersect(a, b, q2, q3) || SegmentsIntersect(a, b, q3, q0);
    }

    private static bool SegmentsIntersect(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        float abC = Cross(b - a, c - a);
        float abD = Cross(b - a, d - a);
        float cdA = Cross(d - c, a - c);
        float cdB = Cross(d - c, b - c);
        if (abC * abD > 0f || cdA * cdB > 0f)
            return false;

        return Mathf.Max(Mathf.Min(a.x, b.x), Mathf.Min(c.x, d.x)) <=
               Mathf.Min(Mathf.Max(a.x, b.x), Mathf.Max(c.x, d.x)) &&
               Mathf.Max(Mathf.Min(a.y, b.y), Mathf.Min(c.y, d.y)) <=
               Mathf.Min(Mathf.Max(a.y, b.y), Mathf.Max(c.y, d.y));
    }

    private static float Cross(Vector2 a, Vector2 b)
    {
        return a.x * b.y - a.y * b.x;
    }

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ValidateMeshCellOverlap()
    {
        Vector3[] vertices = { new(0f, 0f), new(1f, 0f), new(0f, 1f) };
        int[] triangles = { 0, 1, 2 };
        Debug.Assert(MeshOverlapsCell(vertices, triangles, new Vector3(0.25f, 0.25f),
            new Vector3(0.05f, 0f), new Vector3(0f, 0.05f)));
        Debug.Assert(!MeshOverlapsCell(vertices, triangles, new Vector3(2f, 2f),
            new Vector3(0.05f, 0f), new Vector3(0f, 0.05f)));
    }
#endif

    private void ReleaseCell(int cellIndex, int cellX, int cellY, Vector3 cellLocal, Vector3 sawCenter,
        float pressSpeed, float outwardForce, float tangentialForce, float spinDirection, float bladeRadius,
        float sideDamping, float maxVelocity)
    {
        Color32 surfaceColor = _cellColors[cellIndex];
        Color32 releasedColor = _cellReleasedColors[cellIndex];
        ushort typeIndex = _cellReleasedTypes[cellIndex];
        _cellSolid[cellIndex] = 0;
        if (_cellSolidSnapshot != null && (uint)cellIndex < (uint)_cellSolidSnapshot.Length)
            _cellSolidSnapshot[cellIndex] = 0;
        Vector3 releasedWorldPosition = _runtimeParent.TransformPoint(cellLocal);
        EmitCutParticles(releasedWorldPosition, surfaceColor, releasedWorldPosition - sawCenter);
        Entity releasedEntity = QueueReleasedBlockSpawn(cellLocal, releasedColor, typeIndex, sawCenter, pressSpeed, outwardForce,
            tangentialForce, spinDirection, bladeRadius, sideDamping, maxVelocity);
        AttachReleasedDecorationsAtCell(cellIndex, releasedEntity);
        SpawnSeparateReleasedDecorationsAtCell(cellIndex, cellLocal, sawCenter, pressSpeed, outwardForce,
            tangentialForce, spinDirection, bladeRadius, sideDamping, maxVelocity);
        MarkCellChunkDirty(cellX, cellY);
    }
    private void MarkCellChunkDirty(int cellX, int cellY)
    {
        if (_chunks.Count == 0 || _chunkDirty == null || _chunkColumns <= 0)
            return;
        int chunkX = cellX / _chunkSize;
        int chunkY = cellY / _chunkSize;
        int chunkIndex = chunkY * _chunkColumns + chunkX;
        MarkChunkDirty(chunkIndex);
        MarkDecorationChunksDirty(cellY * _gridWidth + cellX);
    }
    private void MarkChunkDirty(int chunkIndex)
    {
        if ((uint)chunkIndex >= (uint)_chunks.Count || _chunkDirty[chunkIndex] != 0)
            return;
        _chunkDirty[chunkIndex] = 1;
        _dirtyChunks.Add(chunkIndex);
    }
    internal bool TryCreatePendingSawPushJob(out SawPushJob job)
    {
        job = default;
        if (!_hasPendingSawPush)
            return false;
        _hasPendingSawPush = false;
        if (_pendingSawRadius <= 0f || !_cellSolid.IsCreated || _runtimeParent == null)
            return false;

        float4x4 worldToLocal = ToFloat4x4(_runtimeParent.worldToLocalMatrix);
        float worldCellSize = GetWorldCellSize();
        float safeMaxVelocity = math.min(_pendingSawMaxVelocity,
            worldCellSize * MaxCellTravelPerStep / math.max(Time.fixedDeltaTime, MinimumPhysicsDeltaTime));

        job = new SawPushJob
        {
            CellSolid = _cellSolid,
            WorldToLocal = worldToLocal,
            Offset = ToFloat3(_offset),
            SawCenter = ToFloat3(_pendingSawCenter),
            PressDirection = ToFloat3(_pendingSawDirection),
            PressSpeed = _pendingSawSpeed,
            OutwardForce = _pendingSawOutwardForce,
            TangentialForce = _pendingSawTangentialForce,
            SpinDirection = _pendingSawSpinDirection,
            BladeRadius = _pendingSawRadius,
            MaxVelocity = safeMaxVelocity,
            CellSize = ToFloat2(_cellSize),
            WorldCellSize = worldCellSize,
            GridWidth = _gridWidth,
            GridHeight = _gridHeight,
            OwnerId = _ownerId
        };

        return true;
    }
    internal bool TryCreateSolidConstraintJob(bool resolveAfterPhysics, out ReleasedBlockSolidConstraintJob job)
    {
        job = default;
        if (!_cellSolid.IsCreated || _runtimeParent == null)
            return false;

        job = new ReleasedBlockSolidConstraintJob
        {
            CellSolid = _cellSolid,
            WorldToLocal = ToFloat4x4(_runtimeParent.worldToLocalMatrix),
            LocalToWorld = ToFloat4x4(_runtimeParent.localToWorldMatrix),
            Offset = ToFloat3(_offset),
            CellSize = ToFloat2(_cellSize),
            GridBoundsMin = new float2(_offset.x, _offset.y) - ToFloat2(_cellSize) * 0.98f,
            GridBoundsMax = new float2(
                _offset.x + (_gridWidth - 1) * _cellSize.x,
                _offset.y + (_gridHeight - 1) * _cellSize.y) + ToFloat2(_cellSize) * 0.98f,
            DeltaTime = Mathf.Max(Time.fixedDeltaTime, MinimumPhysicsDeltaTime),
            LocalRadiusScale = GetMaxLocalUnitsPerWorldUnit(),
            ResolveAfterPhysics = resolveAfterPhysics ? (byte)1 : (byte)0,
            GridWidth = _gridWidth,
            GridHeight = _gridHeight,
            OwnerId = _ownerId
        };
        return true;
    }
    private void QueueSawPush(Vector3 sawCenter, Vector3 pressDirection, float pressSpeed, float outwardForce,
        float tangentialForce, float spinDirection, float bladeRadius, float maxVelocity)
    {
        _hasPendingSawPush = true;
        _pendingSawCenter = sawCenter;
        _pendingSawDirection = pressDirection;
        _pendingSawSpeed = pressSpeed;
        _pendingSawOutwardForce = outwardForce;
        _pendingSawTangentialForce = tangentialForce;
        _pendingSawSpinDirection = spinDirection;
        _pendingSawRadius = bladeRadius;
        _pendingSawMaxVelocity = maxVelocity;
    }
}
