using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public sealed class BreakableObstacle : MonoBehaviour
{
    private static readonly int CrackAmountPropertyId = Shader.PropertyToID("_CrackAmount");

    [Header("Damage")]
    [SerializeField, Min(0.1f)] private float _durability = 1.2f;
    [SerializeField, Range(1, 5)] private int _crackStages = 3;

    private MeshRenderer _meshRenderer;
    private MaterialPropertyBlock _propertyBlock;
    private LevelMapAuthoring _spawner;
    private float _damage;
    private int _visibleCrackStage;

    private void Awake()
    {
        _meshRenderer = GetComponent<MeshRenderer>();
    }

    internal bool ApplySawDamage(float damage)
    {
        if (damage <= 0f)
            return false;

        _damage += damage;
        float progress = Mathf.Clamp01(_damage / _durability);
        int crackStage = Mathf.Min(_crackStages, Mathf.CeilToInt(progress * _crackStages));
        if (crackStage != _visibleCrackStage)
        {
            _visibleCrackStage = crackStage;
            _propertyBlock ??= new MaterialPropertyBlock();
            _meshRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetFloat(CrackAmountPropertyId, crackStage / (float)_crackStages);
            _meshRenderer.SetPropertyBlock(_propertyBlock);
        }

        if (progress < 1f)
            return false;

        Break();
        return true;
    }

    internal void Initialize(LevelMapAuthoring spawner)
    {
        _spawner = spawner;
    }

    private void Break()
    {
        if (_spawner == null)
            Debug.LogError("Breakable obstacle was not registered with its LevelMapAuthoring.", this);
        else
            _spawner.ReleaseResourcesUnderObstacle(this);

        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    [ContextMenu("Validate Breakable Setup")]
    private void ValidateSetup()
    {
        Debug.Assert(GetComponent<MeshFilter>().sharedMesh != null, "Breakable obstacle needs a mesh.", this);
        Debug.Assert(GetComponent<MeshRenderer>().sharedMaterial != null, "Breakable obstacle needs a material.", this);
    }
}
