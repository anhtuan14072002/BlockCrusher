using UnityEngine;
using RenderMaterial = UnityEngine.Material;

public sealed partial class LevelMapSpawner
{
    private void CreateCutParticles()
    {
        // Temporarily disabled while tuning the saw cut feedback.
    }

    private void EmitCutParticles(Vector3 position, Color32 color, Vector3 outward)
    {
        // Temporarily disabled while tuning the saw cut feedback.
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
