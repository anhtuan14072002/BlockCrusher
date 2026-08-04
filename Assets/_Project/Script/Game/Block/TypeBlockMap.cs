using Crusher;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public sealed class LevelBlock : MonoBehaviour
{
    [SerializeField] private TypeBlock _type;
    [SerializeField] private GameObject _releasedPrefab;
    [SerializeField, Min(0.0001f)] private float _releasedScale = 0.12f;
    [SerializeField, Min(0.0001f)] private float _cellSize = 1f;
    
    [SerializeField] private Color _mapColor = new Color(0.42f, 0.31f, 0.23f, 1f);
    [SerializeField] private Color _releasedColor = new Color(0.42f, 0.31f, 0.23f, 1f);

    public TypeBlock CollectibleId => _type;
    public GameObject ReleasedPrefab => _releasedPrefab;
    public float ReleasedScale => _releasedScale;
    public float CellSize => _cellSize;
    public Color32 MapColor => _mapColor;
    public Color32 ReleasedColor => _releasedColor;
}
