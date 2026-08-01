using UnityEngine;
using RenderMaterial = UnityEngine.Material;

public sealed partial class TextureBlockSpawner
{
    private void CreateCutParticles()
    {
        DisposeCutParticles();
        Shader shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null)
            return;

        _cutParticleMaterial = new RenderMaterial(shader);
        GameObject particleObject = new GameObject("CutDust");
        particleObject.transform.SetParent(transform, false);
        _cutParticles = particleObject.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = _cutParticles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 512;
        main.startLifetime = 0.36f;
        main.startSpeed = 0f;
        main.startSize = 0.1f;
        main.gravityModifier = 0.35f;

        ParticleSystem.EmissionModule emission = _cutParticles.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = _cutParticles.shape;
        shape.enabled = false;
        ParticleSystemRenderer renderer = particleObject.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = _cutParticleMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private void EmitCutParticles(Vector3 position, Color32 color, Vector3 outward)
    {
        if (_cutParticles == null)
            return;

        outward.z = 0f;
        if (outward.sqrMagnitude <= 0.0001f)
            outward = Vector3.up;
        else
            outward.Normalize();
        Vector3 tangent = new Vector3(-outward.y, outward.x, 0f);
        EmitCutParticle(position, color, outward * 1.15f + Vector3.up * 0.4f, 0.18f, 0.38f);
        EmitCutParticle(position, color, outward * 0.65f + tangent * 0.75f, 0.13f, 0.32f);
        EmitCutParticle(position, color, outward * 0.65f - tangent * 0.75f, 0.13f, 0.32f);
        EmitCutParticle(position, color, tangent * 0.5f + Vector3.up * 0.2f, 0.1f, 0.26f);
        EmitCutParticle(position, color, -tangent * 0.5f + Vector3.up * 0.2f, 0.1f, 0.26f);
    }

    private void EmitCutParticle(Vector3 position, Color32 color, Vector3 velocity, float size, float lifetime)
    {
        ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams
        {
            position = position,
            velocity = velocity,
            startColor = new Color32(color.r, color.g, color.b, 190),
            startSize = size,
            startLifetime = lifetime
        };
        _cutParticles.Emit(emit, 1);
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
