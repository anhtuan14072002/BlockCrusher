using Crusher;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public sealed class BreakableObstacle : MonoBehaviour
{
    internal const int ShardMeshCount = 4;

    private static readonly int CrackAmountPropertyId = Shader.PropertyToID("_CrackAmount");
    private static Mesh[] _shardMeshes;

    [Header("Damage")]
    [SerializeField, Min(0.1f)] private float _durability = 1.2f;
    [SerializeField, Range(1, 5)] private int _crackStages = 3;

    [Header("Break")]
    [SerializeField, Range(6, 32)] private int _shardCount = 18;
    [SerializeField, Min(0.01f)] private float _releasedScale = 0.16f;
    [SerializeField, Min(0f)] private float _breakForce = 2.5f;
    [SerializeField] private GameObject _releasedPrefab;
    [SerializeField] private TypeBlock _type = TypeBlock.Rock;
    [SerializeField] private Color _releasedColor = new(0.52f, 0.53f, 0.54f, 1f);

    private MeshFilter _meshFilter;
    private MeshRenderer _meshRenderer;
    private MaterialPropertyBlock _propertyBlock;
    private LevelMapAuthoring _spawner;
    private ushort[] _releasedTypeIndices;
    private float _damage;
    private int _visibleCrackStage;

    internal int ShardCount => _shardCount;
    internal float ReleasedScale => _releasedScale;
    internal float BreakForce => _breakForce;
    internal GameObject ReleasedPrefab => _releasedPrefab;
    internal TypeBlock CollectibleType => _type;
    internal Color32 ReleasedColor => _releasedColor;

    private void Awake()
    {
        _meshFilter = GetComponent<MeshFilter>();
        _meshRenderer = GetComponent<MeshRenderer>();
    }

    internal bool ApplySawDamage(float damage, Vector3 sawDirection)
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

        Break(sawDirection);
        return true;
    }

    internal Mesh GetShardMesh(int index)
    {
        EnsureShardMeshes();
        return _shardMeshes[index % _shardMeshes.Length];
    }

    internal ushort GetReleasedTypeIndex(int shardIndex)
    {
        return _releasedTypeIndices[shardIndex % _releasedTypeIndices.Length];
    }

    internal void InitializeReleasedTypes(LevelMapAuthoring spawner, ushort[] releasedTypeIndices)
    {
        _spawner = spawner;
        _releasedTypeIndices = releasedTypeIndices;
    }

    private void Break(Vector3 sawDirection)
    {
        if (_spawner == null || _releasedTypeIndices == null)
            Debug.LogError("Breakable obstacle was not registered with its LevelMapAuthoring.", this);
        else
        {
            _spawner.ReleaseResourcesUnderObstacle(this);
        }

        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    private static void EnsureShardMeshes()
    {
        if (_shardMeshes != null)
            return;

        _shardMeshes = new Mesh[ShardMeshCount];
        for (int i = 0; i < _shardMeshes.Length; i++)
        {
            float skew = 0.12f * i;
            Mesh mesh = new Mesh { name = $"RuntimeRockShard_{i}" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.35f, -0.3f),
                new Vector3(0.45f, -0.45f + skew, -0.25f),
                new Vector3(0.2f - skew, 0.5f, -0.2f),
                new Vector3(-0.25f, 0.25f - skew, 0.45f),
                new Vector3(0.35f, -0.1f, 0.35f)
            };
            mesh.triangles = new[]
            {
                0, 2, 1, 0, 3, 2, 0, 1, 4,
                1, 2, 4, 2, 3, 4, 3, 0, 4
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            _shardMeshes[i] = mesh;
        }
    }

    [ContextMenu("Validate Breakable Setup")]
    private void ValidateSetup()
    {
        Debug.Assert(GetComponent<MeshFilter>().sharedMesh != null, "Breakable obstacle needs a mesh.", this);
        Debug.Assert(GetComponent<MeshRenderer>().sharedMaterial != null, "Breakable obstacle needs a material.", this);
        Debug.Assert(_releasedPrefab != null, "Breakable obstacle needs a released prefab.", this);
        Debug.Assert(_type != TypeBlock.None, "Breakable obstacle needs a collectible type.", this);
    }
}
