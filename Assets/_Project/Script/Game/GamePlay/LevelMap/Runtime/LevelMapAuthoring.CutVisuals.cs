using UnityEngine;
using UnityEngine.Rendering;
using RenderMaterial = UnityEngine.Material;

public sealed partial class LevelMapAuthoring
{
    private void CreateCutParticles()
    {
        DisposeCutParticles();
        if (_runtimeParent == null || _chunkMaterial == null ||
            _terrainFragmentMeshes == null || _terrainFragmentMeshes.Length == 0 ||
            _terrainFragmentMeshes[0] == null)
            return;

        GameObject particleObject = new("Terrain Cut Debris");
        particleObject.layer = gameObject.layer;
        particleObject.transform.SetParent(_runtimeParent, false);
        _cutParticles = particleObject.AddComponent<ParticleSystem>();

        float worldCellSize = Mathf.Max(GetWorldCellSize(), 0.01f);
        ParticleSystem.MainModule main = _cutParticles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = 1400;
        main.startSpeed = 0f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.48f);
        main.startSize = new ParticleSystem.MinMaxCurve(worldCellSize * 0.34f, worldCellSize * 0.68f);
        main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
        main.gravityModifier = new ParticleSystem.MinMaxCurve(0.65f, 1.25f);

        ParticleSystem.EmissionModule emission = _cutParticles.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = _cutParticles.shape;
        shape.enabled = false;

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = _cutParticles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(
                new Keyframe(0f, 0.5f, 4f, 4f),
                new Keyframe(0.12f, 1f),
                new Keyframe(0.72f, 0.82f),
                new Keyframe(1f, 0f, -5f, -5f)));

        ParticleSystem.RotationOverLifetimeModule rotationOverLifetime = _cutParticles.rotationOverLifetime;
        rotationOverLifetime.enabled = true;
        rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(-9f, 9f);

        ParticleSystemRenderer particleRenderer = particleObject.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Mesh;
        particleRenderer.mesh = _terrainFragmentMeshes[0];
        particleRenderer.shadowCastingMode = ShadowCastingMode.Off;
        particleRenderer.receiveShadows = false;
        particleRenderer.sortingOrder = 8;

        _cutParticleMaterial = new RenderMaterial(_chunkMaterial)
        {
            name = "Terrain Cut Debris (Runtime)",
            enableInstancing = true
        };
        if (_cutParticleMaterial.HasProperty("_UseVertexColor"))
            _cutParticleMaterial.SetFloat("_UseVertexColor", 1f);
        if (_cutParticleMaterial.HasProperty("_EdgeStrength"))
            _cutParticleMaterial.SetFloat("_EdgeStrength", 0.16f);
        particleRenderer.sharedMaterial = _cutParticleMaterial;

        _cutParticles.Play();
    }

    private void EmitCutParticles(Vector3 position, Color32 color, Vector3 outward)
    {
        if (_cutParticles == null)
            return;

        float worldCellSize = Mathf.Max(GetWorldCellSize(), 0.01f);
        Vector3 direction = Vector3.ProjectOnPlane(outward, Vector3.forward);
        direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.up;
        direction.y = Mathf.Max(direction.y, 0.12f);
        direction.Normalize();

        // The reference game keeps the removed soil readable as a dense, short burst instead
        // of turning every cell into a long-lived rigid body. Two fragments per removed cell
        // gives that response while the ECS collectible path remains reserved for actual loot.
        int fragmentCount = Random.value < 0.38f ? 3 : 2;
        for (int i = 0; i < fragmentCount; i++)
        {
            Vector2 scatter = Random.insideUnitCircle;
            Vector3 scatterDirection = new(scatter.x, Mathf.Abs(scatter.y) * 0.75f, 0f);
            float burstSpeed = worldCellSize * Random.Range(8.5f, 14f);
            Vector3 velocity = (direction * Random.Range(0.5f, 0.95f) +
                                scatterDirection * 0.52f + Vector3.up * Random.Range(0.12f, 0.34f)) *
                               burstSpeed;

            float shade = Random.Range(0.56f, 0.86f);
            Color32 debrisColor = new(
                (byte)Mathf.Clamp(Mathf.RoundToInt(color.r * shade), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(color.g * shade), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(color.b * shade), 0, 255),
                255);
            ParticleSystem.EmitParams emitParams = new()
            {
                position = position + new Vector3(scatter.x, scatter.y, 0f) * worldCellSize * 0.18f,
                velocity = velocity,
                startColor = debrisColor,
                startLifetime = Random.Range(0.22f, 0.48f),
                startSize = worldCellSize * Random.Range(0.34f, 0.68f),
                rotation3D = new Vector3(
                    Random.Range(-25f, 25f), Random.Range(-25f, 25f), Random.Range(0f, 360f))
            };
            _cutParticles.Emit(emitParams, 1);
        }
    }

    private void DisposeCutParticles()
    {
        if (_cutParticles != null)
        {
            GameObject particleObject = _cutParticles.gameObject;
            particleObject.SetActive(false);
            particleObject.transform.SetParent(null);
            DestroyUnityObject(particleObject);
            _cutParticles = null;
        }
        if (_cutParticleMaterial != null)
        {
            DestroyUnityObject(_cutParticleMaterial);
            _cutParticleMaterial = null;
        }
    }
}
