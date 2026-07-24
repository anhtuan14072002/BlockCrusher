using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public sealed partial class TextureBlockSpawner
{
    private void SpawnMetaballWater()
    {
        if (!_spawnMetaballWater || _metaballParticles == null || _waterClusterCount == 0)
            return;

        int margin = 2;
        List<int> seeds = new List<int>();
        for (int y = margin; y < _gridHeight - margin; y++)
        {
            for (int x = margin; x < _gridWidth - margin; x++)
            {
                int index = y * _gridWidth + x;
                if (_cellSolid[index] != 0)
                    seeds.Add(index);
            }
        }

        int minCells = Mathf.Min(_waterCellsPerClusterMin, _waterCellsPerClusterMax);
        int maxCells = Mathf.Max(_waterCellsPerClusterMin, _waterCellsPerClusterMax);
        List<int> waterCells = new List<int>(_waterClusterCount * maxCells);
        List<int> frontier = new List<int>(maxCells * 4);

        for (int cluster = 0; cluster < _waterClusterCount && seeds.Count > 0; cluster++)
        {
            int seedIndex = UnityEngine.Random.Range(0, seeds.Count);
            int seed = seeds[seedIndex];
            seeds[seedIndex] = seeds[seeds.Count - 1];
            seeds.RemoveAt(seeds.Count - 1);
            if (_cellSolid[seed] == 0)
            {
                cluster--;
                continue;
            }

            int targetCount = UnityEngine.Random.Range(minCells, maxCells + 1);
            int clusterStart = waterCells.Count;
            AddWaterCell(seed, waterCells, frontier, margin);
            while (waterCells.Count - clusterStart < targetCount && frontier.Count > 0)
            {
                int frontierIndex = UnityEngine.Random.Range(0, frontier.Count);
                int cell = frontier[frontierIndex];
                frontier[frontierIndex] = frontier[frontier.Count - 1];
                frontier.RemoveAt(frontier.Count - 1);
                if (_cellSolid[cell] != 0)
                    AddWaterCell(cell, waterCells, frontier, margin);
            }
            frontier.Clear();
        }

        SpawnMetaballWaterBlocks(waterCells);
    }
    private void AddWaterCell(int cell, List<int> waterCells, List<int> frontier, int margin)
    {
        _cellSolid[cell] = 0;
        waterCells.Add(cell);
        int x = cell % _gridWidth;
        int y = cell / _gridWidth;
        AddWaterFrontier(x - 1, y, frontier, margin);
        AddWaterFrontier(x + 1, y, frontier, margin);
        AddWaterFrontier(x, y - 1, frontier, margin);
        AddWaterFrontier(x, y + 1, frontier, margin);
    }
    private void AddWaterFrontier(int x, int y, List<int> frontier, int margin)
    {
        if (x < margin || x >= _gridWidth - margin || y < margin || y >= _gridHeight - margin)
            return;
        int cell = y * _gridWidth + x;
        if (_cellSolid[cell] != 0)
            frontier.Add(cell);
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
                     (_metaballParticles != null && _spawnedWaterCellCount > 0 &&
                      _metaballParticles.particleCount == _spawnedWaterCellCount),
            "Metaball water is not wired or did not spawn.", this);
    }
}
