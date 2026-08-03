using Unity.Mathematics;
using UnityEngine;

public sealed partial class LevelMapSpawner
{
    public bool ReleaseInBox(Matrix4x4 cutLocalToWorld, Bounds cutLocalBounds, Vector3 pressDirection,
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

        int minX = Mathf.Max(0, Mathf.FloorToInt((min.x - _offset.x) / _cellSize));
        int maxX = Mathf.Min(_gridWidth - 1, Mathf.CeilToInt((max.x - _offset.x) / _cellSize));
        int minY = Mathf.Max(0, Mathf.FloorToInt((min.y - _offset.y) / _cellSize));
        int maxY = Mathf.Min(_gridHeight - 1, Mathf.CeilToInt((max.y - _offset.y) / _cellSize));
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
                if (cutLocal.x < boundsMin.x || cutLocal.x > boundsMax.x ||
                    cutLocal.y < boundsMin.y || cutLocal.y > boundsMax.y)
                    continue;

                ReleaseCell(cellIndex, x, y, cellLocal, sawCenter, pressSpeed, outwardForce, tangentialForce,
                    spinDirection, bladeRadius, sideDamping, maxVelocity);
                releasedAny = true;
            }
        }
        return releasedAny;
    }

    private void ReleaseCell(int cellIndex, int cellX, int cellY, Vector3 cellLocal, Vector3 sawCenter,
        float pressSpeed, float outwardForce, float tangentialForce, float spinDirection, float bladeRadius,
        float sideDamping, float maxVelocity)
    {
        Color32 surfaceColor = _cellColors[cellIndex];
        Color32 releasedColor = _cellReleasedColors[cellIndex];
        ushort typeIndex = _cellReleasedTypes[cellIndex];
        _cellSolid[cellIndex] = 0;
        Vector3 releasedWorldPosition = _runtimeParent.TransformPoint(cellLocal);
        EmitCutParticles(releasedWorldPosition, surfaceColor, releasedWorldPosition - sawCenter);
        QueueReleasedBlockSpawn(cellLocal, releasedColor, typeIndex, sawCenter, pressSpeed, outwardForce,
            tangentialForce, spinDirection, bladeRadius, sideDamping, maxVelocity);
        SpawnReleasedDecorationsAtCell(cellIndex, cellLocal, sawCenter, pressSpeed, outwardForce, tangentialForce,
            spinDirection, bladeRadius, sideDamping, maxVelocity);
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
        float safeMaxVelocity = math.min(_pendingSawMaxVelocity,
            _cellSize * MaxCellTravelPerStep / math.max(Time.fixedDeltaTime, MinimumPhysicsDeltaTime));

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
            CellSize = _cellSize,
            GridWidth = _gridWidth,
            GridHeight = _gridHeight,
            OwnerId = _ownerId
        };

        return true;
    }
    internal bool TryCreateSolidConstraintJob(out ReleasedBlockSolidConstraintJob job)
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
            CellSize = _cellSize,
            GridBoundsMin = new float2(_offset.x, _offset.y) - _cellSize * 0.98f,
            GridBoundsMax = new float2(
                _offset.x + (_gridWidth - 1) * _cellSize,
                _offset.y + (_gridHeight - 1) * _cellSize) + _cellSize * 0.98f,
            DeltaTime = Mathf.Max(Time.fixedDeltaTime, MinimumPhysicsDeltaTime),
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
