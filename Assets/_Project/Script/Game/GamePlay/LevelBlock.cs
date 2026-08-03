using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public sealed class LevelBlock : MonoBehaviour
{
    [SerializeField] private string _collectibleId = "dirt";
    [SerializeField] private GameObject _releasedPrefab;
    [SerializeField, Min(0.0001f)] private float _releasedScale = 0.12f;
    [SerializeField, Min(0.0001f)] private float _cellSize = 1f;
    [SerializeField] private Color _mapColor = new Color(0.42f, 0.31f, 0.23f, 1f);
    [SerializeField] private Color _releasedColor = new Color(0.42f, 0.31f, 0.23f, 1f);

    public string CollectibleId => _collectibleId;
    public GameObject ReleasedPrefab => _releasedPrefab;
    public float ReleasedScale => _releasedScale;
    public float CellSize => _cellSize;
    public Color32 MapColor => _mapColor;
    public Color32 ReleasedColor => _releasedColor;

    [ContextMenu("Validate Block Setup")]
    private void ValidateSetup()
    {
        Debug.Assert(_releasedPrefab != null, $"Level block '{name}' needs a released prefab.", this);
        Debug.Assert(!string.IsNullOrWhiteSpace(_collectibleId), $"Level block '{name}' needs a collectible id.", this);
        Debug.Assert(_collectibleId.Length <= 60, $"Level block '{name}' collectible id is limited to 60 characters.", this);
        Debug.Assert(_releasedScale > 0f, $"Level block '{name}' needs a positive released scale.", this);
        Debug.Assert(_cellSize > 0f, $"Level block '{name}' needs a positive cell size.", this);
    }
}
