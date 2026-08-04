using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public sealed partial class LevelMapSpawner
{
    private readonly List<Vector3> _metaballWaterSizes = new(64);

    private void SpawnMetaballWater(BlockWater[] waterMarkers)
    {
        if (!_spawnMetaballWater || _metaballParticles == null)
            return;

        List<Vector3> waterPositions = new List<Vector3>(waterMarkers.Length);
        List<Vector3> waterSizes = new List<Vector3>(waterMarkers.Length);
        float depth = -_chunkColliderDepth * 0.5f;
        for (int i = 0; i < waterMarkers.Length; i++)
        {
            Transform marker = waterMarkers[i].transform;
            if (!TryGetWaterCell(marker.position, out _))
                continue;

            Vector3 localPosition = _runtimeParent.InverseTransformPoint(marker.position);
            localPosition.z = depth;
            waterPositions.Add(_runtimeParent.TransformPoint(localPosition));

            Vector3 worldScale = marker.lossyScale;
            float width = Mathf.Abs(worldScale.x) * _waterParticleScale;
            float height = Mathf.Abs(worldScale.y) * _waterParticleScale;
            waterSizes.Add(new Vector3(width, height, Mathf.Max(width, height)));
        }

        if (waterPositions.Count > 0)
            SpawnMetaballWaterBlocks(waterPositions, waterSizes);
    }

    private bool TryGetWaterCell(Vector3 worldPosition, out int cell)
    {
        Vector3 localPosition = _runtimeParent.InverseTransformPoint(worldPosition);
        int x = Mathf.RoundToInt((localPosition.x - _offset.x) / _cellSize.x);
        int y = Mathf.RoundToInt((localPosition.y - _offset.y) / _cellSize.y);
        if ((uint)x >= (uint)_gridWidth || (uint)y >= (uint)_gridHeight)
        {
            cell = -1;
            return false;
        }

        cell = y * _gridWidth + x;
        _cellSolid[cell] = 0;
        return true;
    }
    private void SpawnMetaballWaterBlocks(List<Vector3> waterPositions, List<Vector3> waterSizes)
    {
        if (!EnsureReleasedBlockResources() || !EnsureEcsReady())
            return;

        ParticleSystem.MainModule main = _metaballParticles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startSize3D = true;
        main.maxParticles = Mathf.Max(1, waterPositions.Count);
        ParticleSystem.EmissionModule emission = _metaballParticles.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = _metaballParticles.shape;
        shape.enabled = false;
        ParticleSystemRenderer renderer = _metaballParticles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = _metaballSourceMaterial;

        float maxVelocity = GetSafePhysicsVelocity(float.MaxValue);
        for (int i = 0; i < waterPositions.Count; i++)
        {
            Entity entity = CreateReleasedBlockEntity(waterPositions[i], new Color32(255, 255, 255, 255), 0,
                Vector3.zero, maxVelocity, true, false);
            _metaballWaterEntities.Add(entity);
            _metaballWaterSizes.Add(waterSizes[i]);
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
        for (int i = _metaballWaterEntities.Count - 1; i >= 0; i--)
        {
            Entity entity = _metaballWaterEntities[i];
            if (!_entityManager.Exists(entity))
            {
                _metaballWaterEntities.RemoveAt(i);
                _metaballWaterSizes.RemoveAt(i);
                continue;
            }

            float3 worldPosition = _entityManager.GetComponentData<LocalTransform>(entity).Position;
            Vector3 position = new Vector3(worldPosition.x, worldPosition.y, worldPosition.z);
            _metaballParticleBuffer[particleCount++] = new ParticleSystem.Particle
            {
                position = position,
                startSize3D = _metaballWaterSizes[i],
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
        _metaballWaterSizes.Clear();
        if (_metaballParticles != null)
            _metaballParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
    [ContextMenu("Validate Metaball Water")]
    private void ValidateMetaballWater()
    {
        Debug.Assert(!_spawnMetaballWater ||
                     FindObjectsByType<BlockWater>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length == 0 ||
                     (_metaballParticles != null && _spawnedWaterCellCount > 0 &&
                      _metaballParticles.particleCount == _spawnedWaterCellCount),
            "Metaball water is not wired or did not spawn.", this);
    }
}
