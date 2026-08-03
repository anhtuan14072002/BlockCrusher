using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public sealed class MapPainterLevelSettings : ScriptableObject
{
    public List<GameObject> PaintPrefabs = new();
    public List<bool> SelectedPaintPrefabs = new();
    public List<GameObject> ObstaclePrefabs = new();
    public List<bool> SelectedObstaclePrefabs = new();
    public List<GameObject> SpecialMaterialPrefabs = new();
    public List<bool> SelectedSpecialMaterialPrefabs = new();
    public List<GameObject> OverlayPrefabs = new();
    public List<bool> SelectedOverlayPrefabs = new();
    public float OverlayScaleMultiplier = 1f;
    public List<GameObject> GridPrefabs = new();
    public int PaintType;
    [FormerlySerializedAs("GridSize")]
    public float PrefabSize = 1f;
    public float Spacing;
    public int GridWidth = 10;
    public int GridHeight = 10;
}
