using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public sealed partial class TextureBlockSpawner
{
    private void SpawnMetaballWater()
    {
        if (!_spawnMetaballWater || _metaballParticles == null)
            return;

        LevelWater[] waterPrefabs = FindObjectsByType<LevelWater>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        List<int> waterCells = new List<int>(waterPrefabs.Length);
        for (int i = 0; i < waterPrefabs.Length; i++)
        {
            if (TryGetWaterCell(waterPrefabs[i].transform.position, out int cell))
                waterCells.Add(cell);
        }

        if (waterCells.Count > 0)
            SpawnMetaballWaterBlocks(waterCells);
    }

    private bool TryGetWaterCell(Vector3 worldPosition, out int cell)
    {
        Vector3 localPosition = _runtimeParent.InverseTransformPoint(worldPosition);
        int x = Mathf.RoundToInt((localPosition.x - _offset.x) / _cellSize);
        int y = Mathf.RoundToInt((localPosition.y - _offset.y) / _cellSize);
        if ((uint)x >= (uint)_gridWidth || (uint)y >= (uint)_gridHeight)
        {
            cell = -1;
            return false;
        }

        cell = y * _gridWidth + x;
        _cellSolid[cell] = 0;
        return true;
    }
    private void SpawnMetaballWaterBlocks(List<int> waterCells)
    {
        if (!EnsureReleasedBlockResources() || !EnsureEcsReady())
            return;

        ParticleSystem.MainModule main = _metaballParticles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = Mathf.Max(1, waterCells.Count);
        ParticleSystem.EmissionModule emission = _metaballParticles.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = _metaballParticles.shape;
        shape.enabled = false;
        ParticleSystemRenderer renderer = _metaballParticles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = _metaballSourceMaterial;

        float depth = -_chunkColliderDepth * 0.5f;
        float maxVelocity = GetSafePhysicsVelocity(float.MaxValue);
        for (int i = 0; i < waterCells.Count; i++)
        {
            int cell = waterCells[i];
            Vector3 position = GetCellLocalPosition(cell % _gridWidth, cell / _gridWidth);
            position.z = depth;
            position = _runtimeParent.TransformPoint(position);
            Entity entity = CreateReleasedBlockEntity(position, new Color32(255, 255, 255, 255),
                Vector3.zero, maxVelocity, true, false);
            _metaballWaterEntities.Add(entity);
        }

        _metaballParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        _metaballParticles.Play(false);
        UpdateMetaballWaterRendering();
    }
    private void UpdateMetaballWaterRendering()
    {
        if (_metaballParticles == null || _metaballWaterEntities.Count == 0 || !EnsureEcsReady())
            return;

        _entityManager.CompleteDependencyBeforeRO<LocalTransform>();
        if (_metaballParticleBuffer == null || _metaballParticleBuffer.Length < _metaballWaterEntities.Count)
            _metaballParticleBuffer = new ParticleSystem.Particle[_metaballWaterEntities.Count];

        int particleCount = 0;
        float size = _cellSize * _waterParticleScale;
        for (int i = _metaballWaterEntities.Count - 1; i >= 0; i--)
        {
            Entity entity = _metaballWaterEntities[i];
            if (!_entityManager.Exists(entity))
            {
                _metaballWaterEntities.RemoveAt(i);
                continue;
            }

            float3 worldPosition = _entityManager.GetComponentData<LocalTransform>(entity).Position;
            Vector3 position = _metaballParticles.transform.InverseTransformPoint(
                new Vector3(worldPosition.x, worldPosition.y, worldPosition.z));
            _metaballParticleBuffer[particleCount++] = new ParticleSystem.Particle
            {
                position = position,
                startSize = size,
                startColor = Color.white,
                remainingLifetime = 86400f,
                startLifetime = 86400f
            };
        }

        _metaballParticles.SetParticles(_metaballParticleBuffer, particleCount);
        _spawnedWaterCellCount = particleCount;
    }
    private void ClearMetaballWater()
    {
        _spawnedWaterCellCount = 0;
        _metaballWaterEntities.Clear();
        if (_metaballParticles != null)
            _metaballParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
    [ContextMenu("Validate Metaball Water")]
    private void ValidateMetaballWater()
    {
        Debug.Assert(!_spawnMetaballWater ||
                     FindObjectsByType<LevelWater>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length == 0 ||
                     (_metaballParticles != null && _spawnedWaterCellCount > 0 &&
                      _metaballParticles.particleCount == _spawnedWaterCellCount),
            "Metaball water is not wired or did not spawn.", this);
    }
}
