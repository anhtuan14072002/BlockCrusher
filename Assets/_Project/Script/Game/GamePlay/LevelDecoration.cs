using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public sealed class LevelDecoration : MonoBehaviour
{
    [SerializeField] private Color _color = UnityEngine.Color.white;
    [SerializeField] private string _collectibleId;
    [SerializeField] private GameObject _releasedPrefab;
    [SerializeField, Min(0.0001f)] private float _releasedScale = 0.12f;
    [SerializeField] private Color _releasedColor = UnityEngine.Color.white;

    public Color32 Tint => _color;
    public string CollectibleId => _collectibleId;
    public GameObject ReleasedPrefab => _releasedPrefab;
    public float ReleasedScale => _releasedScale;
    public Color32 ReleasedColor => _releasedColor;
    public bool ReleasesCollectible => _releasedPrefab != null && !string.IsNullOrWhiteSpace(_collectibleId);
}
