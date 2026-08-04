using Crusher;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public sealed class LevelDecoration : MonoBehaviour
{
    [SerializeField] private Color _color = Color.white;
    [SerializeField] private TypeBlock _type;
    [SerializeField] private GameObject _releasedPrefab;
    [SerializeField, Min(0.0001f)] private float _releasedScale = 0.12f;
    [SerializeField] private Color _releasedColor = Color.white;

    public Color32 Tint => _color;
    public TypeBlock BlockType => _type;
    public GameObject ReleasedPrefab => _releasedPrefab;
    public float ReleasedScale => _releasedScale;
    public Color32 ReleasedColor => _releasedColor;
    public bool ReleasesCollectible => _releasedPrefab != null && _type != TypeBlock.None;
}
