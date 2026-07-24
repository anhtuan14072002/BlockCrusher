using Unity.Mathematics;
using UnityEngine;

public sealed partial class TextureBlockSpawner
{
    public bool ReleaseAtWorld(Vector3 worldPoint, Vector3 pressDirection, float pressSpeed, float outwardForce,
        float tangentialForce, float spinDirection, float bladeRadius, float sideDamping, float maxVelocity)
    {
        if (!_cellSolid.IsCreated || _runtimeParent == null)
            return false;
        QueueSawPush(worldPoint, pressDirection, pressSpeed, outwardForce, tangentialForce, spinDirection,
            bladeRadius, maxVelocity);
        Vector3 localPoint = _runtimeParent.InverseTransformPoint(worldPoint);
        float radiusSqr = _sawReleaseRadius * _sawReleaseRadius;
        int centerX = Mathf.RoundToInt((localPoint.x - _offset.x) / _cellSize);
        int centerY = Mathf.RoundToInt((localPoint.y - _offset.y) / _cellSize);
        int radiusCells = Mathf.Max(1, Mathf.CeilToInt(_sawReleaseRadius / _cellSize));
        int minX = Mathf.Max(0, centerX - radiusCells);
        int maxX = Mathf.Min(_gridWidth - 1, centerX + radiusCells);
        int minY = Mathf.Max(0, centerY - radiusCells);
        int maxY = Mathf.Min(_gridHeight - 1, centerY + radiusCells);
        bool releasedAny = false;
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                Vector3 cellLocal = GetCellLocalPosition(x, y);
                Vector3 delta = cellLocal - localPoint;
                if (delta.x * delta.x + delta.y * delta.y > radiusSqr)
                    continue;
                int cellIndex = y * _gridWidth + x;
                if (_cellSolid[cellIndex] == 0)
                    continue;
                Color32 color = _cellColors[cellIndex];
                _cellSolid[cellIndex] = 0;
                if (TryConsumePhysicsDebrisBudget())
                {
                    QueueReleasedBlockSpawn(cellLocal, color, worldPoint, pressDirection, pressSpeed, outwardForce,
                        tangentialForce, spinDirection, bladeRadius, sideDamping, maxVelocity);
                }
                MarkCellChunkDirty(x, y);
                releasedAny = true;
            }
        }
        return releasedAny;
    }
    public bool HasSolidAtWorld(Vector3 worldPoint, float radius)
    {
        if (!_cellSolid.IsCreated || _runtimeParent == null)
            return false;
        Vector3 localPoint = _runtimeParent.InverseTransformPoint(worldPoint);
        float radiusSqr = radius * radius;
        int centerX = Mathf.RoundToInt((localPoint.x - _offset.x) / _cellSize);
        int centerY = Mathf.RoundToInt((localPoint.y - _offset.y) / _cellSize);
        int radiusCells = Mathf.Max(1, Mathf.CeilToInt(radius / _cellSize));
        int minX = Mathf.Max(0, centerX - radiusCells);
        int maxX = Mathf.Min(_gridWidth - 1, centerX + radiusCells);
        int minY = Mathf.Max(0, centerY - radiusCells);
        int maxY = Mathf.Min(_gridHeight - 1, centerY + radiusCells);
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                int cellIndex = y * _gridWidth + x;
                if (_cellSolid[cellIndex] == 0)
                    continue;
                Vector3 cellLocal = GetCellLocalPosition(x, y);
                Vector3 delta = cellLocal - localPoint;
                if (delta.x * delta.x + delta.y * delta.y <= radiusSqr)
                    return true;
            }
        }
        return false;
    }
    private void MarkCellChunkDirty(int cellX, int cellY)
    {
        if (_chunks.Count == 0 || _chunkDirty == null || _chunkColumns <= 0)
            return;
        int chunkX = cellX / _chunkSize;
        int chunkY = cellY / _chunkSize;
        int chunkIndex = chunkY * _chunkColumns + chunkX;
        if ((uint)chunkIndex >= (uint)_chunks.Count || _chunkDirty[chunkIndex] != 0)
            return;
        _chunkDirty[chunkIndex] = 1;
        _dirtyChunks.Add(chunkIndex);
    }
    private bool TryConsumePhysicsDebrisBudget()
    {
        if (_maxPhysicsDebrisPerFrame == 0)
            return true;
        int frame = Time.frameCount;
        if (_physicsDebrisFrame != frame)
        {
            _physicsDebrisFrame = frame;
            _physicsDebrisSpawnedThisFrame = 0;
        }
        if (_physicsDebrisSpawnedThisFrame >= _maxPhysicsDebrisPerFrame)
            return false;
        _physicsDebrisSpawnedThisFrame++;
        return true;
    }
    internal bool TryCreatePendingSawPushJob(out SawPushJob job)
    {
        job = default;
        if (!_hasPendingSawPush)
            return false;
        _hasPendingSawPush = false;
        if (_pendingSawRadius <= 0f || !_cellSolid.IsCreated || _runtimeParent == null)
            return false;

        Matrix4x4 matrix = _runtimeParent.worldToLocalMatrix;
        float4x4 worldToLocal = new float4x4(ToFloat4(matrix.GetColumn(0)), ToFloat4(matrix.GetColumn(1)),
            ToFloat4(matrix.GetColumn(2)), ToFloat4(matrix.GetColumn(3)));
        float safeMaxVelocity = math.min(_pendingSawMaxVelocity,
            _cellSize * 0.75f / math.max(Time.fixedDeltaTime, 0.001f));

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

        Matrix4x4 worldToLocalMatrix = _runtimeParent.worldToLocalMatrix;
        Matrix4x4 localToWorldMatrix = _runtimeParent.localToWorldMatrix;
        job = new ReleasedBlockSolidConstraintJob
        {
            CellSolid = _cellSolid,
            WorldToLocal = new float4x4(ToFloat4(worldToLocalMatrix.GetColumn(0)),
                ToFloat4(worldToLocalMatrix.GetColumn(1)), ToFloat4(worldToLocalMatrix.GetColumn(2)),
                ToFloat4(worldToLocalMatrix.GetColumn(3))),
            LocalToWorld = new float4x4(ToFloat4(localToWorldMatrix.GetColumn(0)),
                ToFloat4(localToWorldMatrix.GetColumn(1)), ToFloat4(localToWorldMatrix.GetColumn(2)),
                ToFloat4(localToWorldMatrix.GetColumn(3))),
            Offset = ToFloat3(_offset),
            CellSize = _cellSize,
            BlockRadius = _cellSize * 0.48f,
            GridBoundsMin = new float2(_offset.x, _offset.y) - _cellSize * 0.98f,
            GridBoundsMax = new float2(
                _offset.x + (_gridWidth - 1) * _cellSize,
                _offset.y + (_gridHeight - 1) * _cellSize) + _cellSize * 0.98f,
            DeltaTime = Mathf.Max(Time.fixedDeltaTime, 0.001f),
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
