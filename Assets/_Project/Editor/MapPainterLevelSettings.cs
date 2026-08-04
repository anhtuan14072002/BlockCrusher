using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public sealed class MapPainterGridColorLayer
{
    public int FromDepth;
    public int ToDepth;
    public Color Color = Color.white;

    public MapPainterGridColorLayer()
    {
    }

    public MapPainterGridColorLayer(int fromDepth, int toDepth, Color color)
    {
        FromDepth = fromDepth;
        ToDepth = toDepth;
        Color = color;
    }

    public MapPainterGridColorLayer(MapPainterGridColorLayer source)
        : this(source.FromDepth, source.ToDepth, source.Color)
    {
    }
}

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
    public float OverlayLocalZ = -0.31f;
    public bool HasOverlayLocalZ;
    public List<GameObject> GridPrefabs = new();
    public List<MapPainterGridColorLayer> GridColorLayers = new();
    public int PaintType;
    [FormerlySerializedAs("GridSize")]
    public float PrefabSize = 1f;
    public Vector3 Scale = Vector3.one;
    public bool HasScale;
    public float Spacing;
    public int GridWidth = 10;
    public int GridHeight = 10;
}
