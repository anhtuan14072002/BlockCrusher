using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class MapPainterWindow : EditorWindow
{
    private enum PaintType
    {
        Block,
        Obstacle,
        SpecialMaterial,
        Overlay
    }

    private static readonly Vector3 DefaultScale = Vector3.one;
    private const float DefaultOverlayLocalZ = -0.31f;
    private const string LevelFolder = "Assets/_Project/Resources/Level";
    private const string LevelSettingsFolder = "Assets/_Project/Editor/MapPainterData";
    private static readonly string[] EditModeLabels = { "None", "Edit" };
    private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
    private static readonly int UseVertexColorPropertyId = Shader.PropertyToID("_UseVertexColor");

    [SerializeField] private List<GameObject> _prefabs = new();
    [SerializeField] private List<bool> _selectedPrefabs = new();
    [SerializeField] private List<GameObject> _obstaclePrefabs = new();
    [SerializeField] private List<bool> _selectedObstaclePrefabs = new();
    [SerializeField] private List<GameObject> _specialMaterialPrefabs = new();
    [SerializeField] private List<bool> _selectedSpecialMaterialPrefabs = new();
    [SerializeField] private List<GameObject> _overlayPrefabs = new();
    [SerializeField] private List<bool> _selectedOverlayPrefabs = new();
    [SerializeField] private float _overlayScaleMultiplier = 1f;
    [SerializeField] private float _overlayLocalZ = DefaultOverlayLocalZ;
    [SerializeField] private List<GameObject> _gridPrefabs = new();
    [SerializeField] private List<MapPainterGridColorLayer> _gridColorLayers = new();
    [SerializeField] private Transform _mapRoot;
    [SerializeField] private Vector3 _scale = DefaultScale;
    [SerializeField] private float _spacing;
    [SerializeField] private int _gridWidth = 10;
    [SerializeField] private int _gridHeight = 10;
    [SerializeField] private string _levelName = "level_1";
    [SerializeField] private int _loadLevelNumber = 1;
    [SerializeField] private bool _paintEnabled = true;
    [SerializeField] private PaintType _paintType;

    private readonly System.Random _random = new();
    private MaterialPropertyBlock _previewProperties;
    private Vector2 _scrollPosition;
    private Vector2Int _lastPaintedCell = new(int.MinValue, int.MinValue);
    private int _undoGroup = -1;

    [MenuItem("Tools/Map Painter")]
    private static void Open()
    {
        GetWindow<MapPainterWindow>("Map Painter");
    }

    [InitializeOnLoadMethod]
    private static void InitializePrefabPreview()
    {
        PrefabStage.prefabStageOpened -= OnPrefabStageOpened;
        PrefabStage.prefabStageOpened += OnPrefabStageOpened;
        EditorApplication.delayCall += ApplyCurrentPrefabPreview;
    }

    private static void OnPrefabStageOpened(PrefabStage prefabStage)
    {
        EditorApplication.delayCall += () =>
        {
            if (prefabStage == PrefabStageUtility.GetCurrentPrefabStage())
                ApplyPreviewColors(prefabStage.prefabContentsRoot.transform);
        };
    }

    private static void ApplyCurrentPrefabPreview()
    {
        PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
        if (prefabStage != null)
            ApplyPreviewColors(prefabStage.prefabContentsRoot.transform);
    }

    private void OnEnable()
    {
        _previewProperties = new MaterialPropertyBlock();
        EnsureSelectionCount();
        BindCurrentPrefabRoot();
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        EndStroke();
    }

    private void OnGUI()
    {
        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
        EditorGUILayout.LabelField("Map", EditorStyles.boldLabel);
        _mapRoot = (Transform)EditorGUILayout.ObjectField("Map Root", _mapRoot, typeof(Transform), true);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (_mapRoot != null && GUILayout.Button("Select Root"))
                Selection.activeTransform = _mapRoot;
        }

        _levelName = EditorGUILayout.TextField("Level Prefab Name", _levelName);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Create Level"))
                CreateLevel();

            if (GUILayout.Button("Save Level Prefab"))
                SaveLevelPrefab();
        }

        EditorGUILayout.LabelField($"Save to: {LevelFolder}/{BuildLevelName(_levelName)}.prefab",
            EditorStyles.miniLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            _loadLevelNumber = Mathf.Max(1, EditorGUILayout.IntField("Load Level Number", _loadLevelNumber));
            if (GUILayout.Button("Load Level", GUILayout.Width(100f)))
                LoadLevel();
        }

        _scale = Max(EditorGUILayout.Vector3Field("Scale", _scale), 0.01f);
        _spacing = Mathf.Max(0f, EditorGUILayout.FloatField("Spacing", _spacing));
        bool wasPaintEnabled = _paintEnabled;
        _paintEnabled = GUILayout.Toolbar(_paintEnabled ? 1 : 0, EditModeLabels) == 1;
        if (wasPaintEnabled && !_paintEnabled)
            EndStroke();
        _paintType = (PaintType)EditorGUILayout.EnumPopup("Paint Type", _paintType);
        EditorGUILayout.LabelField("Erase: hold right mouse and drag.", EditorStyles.miniLabel);

        DrawGridGenerator();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Block Prefabs", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Drag prefab vào danh sách. Chọn một hoặc nhiều prefab; nếu chọn nhiều, mỗi ô sẽ random một prefab. " +
            "Giữ chuột trái trong Scene để vẽ. " +
            "Giữ Shift + chuột trái để xoá.", MessageType.Info);

        EnsureSelectionCount();
        DrawPrefabDropArea(_prefabs, _selectedPrefabs, PaintType.Block);
        for (int i = 0; i < _prefabs.Count; i++)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                bool wasSelected = _selectedPrefabs[i];
                _selectedPrefabs[i] = GUILayout.Toggle(
                    _selectedPrefabs[i], GUIContent.none, GUILayout.Width(20f));
                if (!wasSelected && _selectedPrefabs[i])
                    _paintType = PaintType.Block;

                _prefabs[i] = (GameObject)EditorGUILayout.ObjectField(
                    _prefabs[i], typeof(GameObject), false);

                if (GUILayout.Button("-", GUILayout.Width(24f)))
                {
                    _prefabs.RemoveAt(i);
                    _selectedPrefabs.RemoveAt(i);
                    GUIUtility.ExitGUI();
                }
            }
        }
        if (GUILayout.Button("Add Prefab Slot"))
        {
            _prefabs.Add(null);
            _selectedPrefabs.Add(_prefabs.Count == 1);
            _paintType = PaintType.Block;
        }

        DrawAdditionalPrefabList(
            "Obstacle Prefabs", "Add Obstacle Prefab Slot", PaintType.Obstacle,
            _obstaclePrefabs, _selectedObstaclePrefabs);
        DrawAdditionalPrefabList(
            "Water / Special Prefabs", "Add Water / Special Prefab Slot", PaintType.SpecialMaterial,
            _specialMaterialPrefabs, _selectedSpecialMaterialPrefabs);
        DrawAdditionalPrefabList(
            $"Overlay Block Prefabs (Z = {_overlayLocalZ:0.###})", "Add Overlay Block Prefab Slot", PaintType.Overlay,
            _overlayPrefabs, _selectedOverlayPrefabs);
        _overlayLocalZ = EditorGUILayout.FloatField("Overlay Z", _overlayLocalZ);
        _overlayScaleMultiplier = Mathf.Max(
            0.01f, EditorGUILayout.FloatField("Overlay Scale Multiplier", _overlayScaleMultiplier));
        EditorGUILayout.EndScrollView();
    }

    private void DrawAdditionalPrefabList(
        string title, string addButtonLabel, PaintType paintType,
        List<GameObject> prefabs, List<bool> selectedPrefabs)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        DrawPrefabDropArea(prefabs, selectedPrefabs, paintType);
        for (int i = 0; i < prefabs.Count; i++)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                bool wasSelected = selectedPrefabs[i];
                selectedPrefabs[i] = GUILayout.Toggle(
                    selectedPrefabs[i], GUIContent.none, GUILayout.Width(20f));
                if (!wasSelected && selectedPrefabs[i])
                    _paintType = paintType;
                prefabs[i] = (GameObject)EditorGUILayout.ObjectField(prefabs[i], typeof(GameObject), false);

                if (GUILayout.Button("-", GUILayout.Width(24f)))
                {
                    prefabs.RemoveAt(i);
                    selectedPrefabs.RemoveAt(i);
                    GUIUtility.ExitGUI();
                }
            }
        }

        if (GUILayout.Button(addButtonLabel))
        {
            prefabs.Add(null);
            selectedPrefabs.Add(prefabs.Count == 1);
            _paintType = paintType;
        }
    }

    private void DrawGridGenerator()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Grid Generator", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Drag prefab riêng để tạo lưới. Một prefab sẽ fill toàn bộ lưới; nhiều prefab sẽ random từng ô.",
            MessageType.Info);

        _gridWidth = Mathf.Clamp(EditorGUILayout.IntField("Grid Width", _gridWidth), 1, 200);
        _gridHeight = Mathf.Clamp(EditorGUILayout.IntField("Grid Height", _gridHeight), 1, 200);
        DrawGridColorLayers();
        DrawPrefabDropArea(_gridPrefabs, null, PaintType.Block);

        for (int i = 0; i < _gridPrefabs.Count; i++)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                _gridPrefabs[i] = (GameObject)EditorGUILayout.ObjectField(
                    _gridPrefabs[i], typeof(GameObject), false);

                if (GUILayout.Button("-", GUILayout.Width(24f)))
                {
                    _gridPrefabs.RemoveAt(i);
                    GUIUtility.ExitGUI();
                }
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Add Grid Prefab Slot"))
                _gridPrefabs.Add(null);

            using (new EditorGUI.DisabledScope(_mapRoot == null))
            {
                if (GUILayout.Button("Generate Grid"))
                    GenerateGrid();
            }
        }

        using (new EditorGUI.DisabledScope(_mapRoot == null || CountValidPrefabs(_gridPrefabs) < 2))
        {
            if (GUILayout.Button("Randomize Existing Grid"))
                RandomizeExistingGrid();
        }
    }

    private void DrawGridColorLayers()
    {
        EditorGUILayout.Space(2f);
        EditorGUILayout.LabelField("Grid Color Layers", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Depth 0 is the top row. Ranges include both ends; the first matching layer is used.",
            MessageType.None);

        for (int i = 0; i < _gridColorLayers.Count; i++)
        {
            MapPainterGridColorLayer layer = _gridColorLayers[i];
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"Layer {i + 1}", EditorStyles.boldLabel);
                    if (GUILayout.Button("-", GUILayout.Width(24f)))
                    {
                        _gridColorLayers.RemoveAt(i);
                        GUIUtility.ExitGUI();
                    }
                }

                layer.FromDepth = Mathf.Clamp(
                    EditorGUILayout.IntField("From Depth", layer.FromDepth), 0, _gridHeight - 1);
                layer.ToDepth = Mathf.Clamp(
                    EditorGUILayout.IntField("To Depth", layer.ToDepth), layer.FromDepth, _gridHeight - 1);
                layer.Color = EditorGUILayout.ColorField("Color", layer.Color);
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Add Color Layer"))
            {
                int fromDepth = _gridColorLayers.Count == 0
                    ? 0
                    : Mathf.Min(_gridColorLayers[_gridColorLayers.Count - 1].ToDepth + 1, _gridHeight - 1);
                _gridColorLayers.Add(new MapPainterGridColorLayer(fromDepth, _gridHeight - 1, Color.white));
            }

            using (new EditorGUI.DisabledScope(_mapRoot == null || _gridColorLayers.Count == 0))
            {
                if (GUILayout.Button("Apply Colors To Existing Grid"))
                    ApplyGridColorsToExisting();
            }
        }
    }

    private void DrawPrefabDropArea(
        List<GameObject> prefabs, List<bool> selectedPrefabs, PaintType paintType)
    {
        Rect dropArea = GUILayoutUtility.GetRect(0f, 34f, GUILayout.ExpandWidth(true));
        GUI.Box(dropArea, "Drop Multiple Prefabs Here", EditorStyles.helpBox);

        Event current = Event.current;
        if (!dropArea.Contains(current.mousePosition) ||
            (current.type != EventType.DragUpdated && current.type != EventType.DragPerform))
            return;

        DragAndDrop.visualMode = HasPrefabAssetReference(DragAndDrop.objectReferences)
            ? DragAndDropVisualMode.Copy
            : DragAndDropVisualMode.Rejected;
        if (current.type == EventType.DragPerform && DragAndDrop.visualMode == DragAndDropVisualMode.Copy)
        {
            DragAndDrop.AcceptDrag();
            if (AddPrefabReferences(DragAndDrop.objectReferences, prefabs, selectedPrefabs) > 0 &&
                selectedPrefabs != null)
                _paintType = paintType;
        }

        current.Use();
    }

    private static bool HasPrefabAssetReference(UnityEngine.Object[] references)
    {
        for (int i = 0; i < references.Length; i++)
        {
            if (references[i] is GameObject prefab && PrefabUtility.IsPartOfPrefabAsset(prefab))
                return true;
        }

        return false;
    }

    private static int AddPrefabReferences(
        UnityEngine.Object[] references, List<GameObject> prefabs, List<bool> selectedPrefabs)
    {
        int addedCount = 0;
        for (int i = 0; i < references.Length; i++)
        {
            if (references[i] is GameObject prefab && PrefabUtility.IsPartOfPrefabAsset(prefab) &&
                TryAddPrefab(prefab, prefabs, selectedPrefabs))
                addedCount++;
        }

        return addedCount;
    }

    private static bool TryAddPrefab(
        GameObject prefab, List<GameObject> prefabs, List<bool> selectedPrefabs)
    {
        if (prefab == null || prefabs.Contains(prefab))
            return false;

        int emptyIndex = prefabs.IndexOf(null);
        if (emptyIndex >= 0)
        {
            prefabs[emptyIndex] = prefab;
            if (selectedPrefabs != null)
                selectedPrefabs[emptyIndex] = true;
            return true;
        }

        prefabs.Add(prefab);
        selectedPrefabs?.Add(true);
        return true;
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        ApplySelectedPreviewColor();

        if (!_paintEnabled || _mapRoot == null || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        Event current = Event.current;
        if (current.alt)
            return;

        if (!TryGetCell(current.mousePosition, out Vector2Int cell, out Vector3 localPosition))
            return;

        bool erase = IsEraseInput(current.button, current.shift);
        DrawPreview(GetPaintLocalPosition(localPosition, _paintType, _overlayLocalZ), erase);

        if (current.type == EventType.Layout)
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

        if (current.button != 0 && current.button != 1)
            return;

        if (current.type == EventType.MouseDown)
        {
            BeginStroke(erase ? "Erase Map Blocks" : "Paint Map Blocks");
            ApplyCell(cell, localPosition, erase);
            current.Use();
        }
        else if (current.type == EventType.MouseDrag)
        {
            ApplyCell(cell, localPosition, erase);
            current.Use();
        }
        else if (current.type == EventType.MouseUp)
        {
            EndStroke();
            current.Use();
        }

        sceneView.Repaint();
    }

    private bool TryGetCell(Vector2 mousePosition, out Vector2Int cell, out Vector3 localPosition)
    {
        Transform root = _mapRoot;
        Vector3 planePosition = root != null ? root.position : Vector3.zero;
        Vector3 planeNormal = root != null ? root.forward : Vector3.forward;
        Plane plane = new Plane(planeNormal, planePosition);
        Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);

        if (!plane.Raycast(ray, out float distance))
        {
            cell = default;
            localPosition = default;
            return false;
        }

        Vector3 worldPosition = ray.GetPoint(distance);
        Vector3 rawLocal = root != null ? root.InverseTransformPoint(worldPosition) : worldPosition;
        Vector2 cellStep = GetCellStep(_scale, _spacing);
        cell = new Vector2Int(
            Mathf.RoundToInt(rawLocal.x / cellStep.x),
            Mathf.RoundToInt(rawLocal.y / cellStep.y));
        localPosition = new Vector3(cell.x * cellStep.x, cell.y * cellStep.y, 0f);
        return true;
    }

    private void ApplyCell(Vector2Int cell, Vector3 localPosition, bool erase)
    {
        if (cell == _lastPaintedCell)
            return;

        _lastPaintedCell = cell;
        localPosition = GetPaintLocalPosition(localPosition, _paintType, _overlayLocalZ);
        if (erase)
        {
            EraseAt(localPosition);
            return;
        }

        GameObject prefab = GetRandomSelectedPrefab();
        if (prefab == null)
            return;

        bool useGridColorLayer = _paintType == PaintType.Block && _gridPrefabs.Contains(prefab);
        if (useGridColorLayer)
            prefab = GetGridPrefabForCell(cell.x, cell.y);

        bool isWater = prefab.GetComponentInChildren<BlockWater>(true) != null;
        if ((_paintType == PaintType.Overlay && FindBlockAt(localPosition) != null) ||
            (isWater && HasComponentAt<BlockWater>(localPosition)) ||
            (!isWater && _paintType == PaintType.Block && HasBlockAt(localPosition)))
            return;

        GameObject instance = CreateBlock(prefab, localPosition, _paintType != PaintType.Overlay);
        if (useGridColorLayer)
            ApplyGridLayerColor(instance, cell.y);

        if (_paintType == PaintType.Overlay)
        {
            instance.transform.localScale *= _overlayScaleMultiplier;
            Vector3 eulerAngles = instance.transform.localEulerAngles;
            eulerAngles.z = GetRandomZAngle(_random);
            instance.transform.localEulerAngles = eulerAngles;
        }
    }

    private GameObject CreateBlock(GameObject prefab, Vector3 localPosition, bool applyScale = true)
    {
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, _mapRoot);
        Undo.RegisterCreatedObjectUndo(instance, "Paint Map Block");
        instance.transform.localPosition = localPosition;
        if (applyScale)
            instance.transform.localScale = _scale;
        ApplyPreviewColor(instance);
        return instance;
    }

    private void GenerateGrid()
    {
        if (_mapRoot == null || CountValidPrefabs(_gridPrefabs) == 0)
        {
            EditorUtility.DisplayDialog("Map Painter", "Assign a Map Root and at least one Grid Prefab.", "OK");
            return;
        }

        HashSet<Vector2Int> occupiedCells = new(_mapRoot.childCount);
        Vector2 cellStep = GetCellStep(_scale, _spacing);
        for (int i = 0; i < _mapRoot.childCount; i++)
        {
            Vector3 position = _mapRoot.GetChild(i).localPosition;
            occupiedCells.Add(new Vector2Int(
                Mathf.RoundToInt(position.x / cellStep.x),
                Mathf.RoundToInt(position.y / cellStep.y)));
        }

        BeginStroke("Generate Map Grid");
        for (int y = 0; y < _gridHeight; y++)
        {
            for (int x = 0; x < _gridWidth; x++)
            {
                Vector2Int cell = new(x, y);
                if (!occupiedCells.Add(cell))
                    continue;

                GameObject instance = CreateBlock(
                    GetGridPrefabForCell(x, y), GetGridLocalPosition(x, y, cellStep));
                ApplyGridLayerColor(instance, y);
            }
        }
        EndStroke();
        SceneView.RepaintAll();
    }

    private void RandomizeExistingGrid()
    {
        GameObject template = GetFirstValidPrefab(_gridPrefabs);
        TypeBlockMap templateBlockMap = template != null ? template.GetComponent<TypeBlockMap>() : null;
        if (templateBlockMap == null)
        {
            EditorUtility.DisplayDialog("Map Painter", "Grid Prefabs need TypeBlockMap components.", "OK");
            return;
        }

        BeginStroke("Randomize Map Grid");
        Undo.RegisterFullObjectHierarchyUndo(_mapRoot.gameObject, "Randomize Map Grid");
        Vector2 cellStep = GetCellStep(_scale, _spacing);
        int replacedCount = 0;
        for (int i = 0; i < _mapRoot.childCount; i++)
        {
            GameObject instance = _mapRoot.GetChild(i).gameObject;
            TypeBlockMap blockMap = instance.GetComponent<TypeBlockMap>();
            if (blockMap == null || blockMap.BlockType != templateBlockMap.BlockType)
                continue;

            Vector3 position = instance.transform.localPosition;
            int cellX = Mathf.RoundToInt(position.x / cellStep.x);
            int cellY = Mathf.RoundToInt(position.y / cellStep.y);
            GameObject prefab = GetGridPrefabForCell(cellX, cellY);
            if (prefab == null)
                continue;

            PrefabUtility.ReplacePrefabAssetOfPrefabInstance(
                instance, prefab, InteractionMode.AutomatedAction);
            ApplyGridLayerColor(instance, cellY);
            ApplyPreviewColor(instance);
            replacedCount++;
        }
        EndStroke();
        EditorUtility.SetDirty(_mapRoot.gameObject);
        SceneView.RepaintAll();
        Debug.Log($"Randomized {replacedCount} grid blocks.", _mapRoot);
    }

    private void ApplyGridColorsToExisting()
    {
        Undo.RegisterFullObjectHierarchyUndo(_mapRoot.gameObject, "Apply Grid Color Layers");
        Vector2 cellStep = GetCellStep(_scale, _spacing);
        int coloredCount = 0;
        for (int i = 0; i < _mapRoot.childCount; i++)
        {
            GameObject instance = _mapRoot.GetChild(i).gameObject;
            if (instance.GetComponent<TypeBlockMap>() == null)
                continue;

            int cellY = Mathf.RoundToInt(instance.transform.localPosition.y / cellStep.y);
            if (ApplyGridLayerColor(instance, cellY))
                coloredCount++;
        }

        EditorUtility.SetDirty(_mapRoot.gameObject);
        SceneView.RepaintAll();
        Debug.Log($"Applied grid color layers to {coloredCount} blocks.", _mapRoot);
    }

    private bool ApplyGridLayerColor(GameObject instance, int cellY)
    {
        if (!TryGetGridLayerColor(_gridColorLayers, _gridHeight, cellY, out Color color))
            return false;

        TypeBlockMap blockMap = instance.GetComponent<TypeBlockMap>();
        if (blockMap == null)
            return false;

        SerializedObject serializedBlock = new(blockMap);
        serializedBlock.FindProperty("_mapColor").colorValue = color;
        serializedBlock.FindProperty("_releasedColor").colorValue = color;
        serializedBlock.ApplyModifiedPropertiesWithoutUndo();
        ApplyPreviewColor(instance);
        return true;
    }

    private static bool TryGetGridLayerColor(
        List<MapPainterGridColorLayer> layers, int gridHeight, int cellY, out Color color)
    {
        int depth = gridHeight - 1 - cellY;
        for (int i = 0; i < layers.Count; i++)
        {
            MapPainterGridColorLayer layer = layers[i];
            if (depth < layer.FromDepth || depth > layer.ToDepth)
                continue;

            color = layer.Color;
            return true;
        }

        color = default;
        return false;
    }

    private void EraseAt(Vector3 localPosition)
    {
        Transform block = FindBlockAt(localPosition);
        if (block != null)
            Undo.DestroyObjectImmediate(block.gameObject);
    }

    private bool HasBlockAt(Vector3 localPosition)
    {
        return FindBlockAt(localPosition) != null;
    }

    private bool HasComponentAt<T>(Vector3 localPosition) where T : Component
    {
        if (_mapRoot == null)
            return false;

        Vector2 cellStep = GetCellStep(_scale, _spacing);
        float tolerance = Mathf.Min(cellStep.x, cellStep.y) * 0.01f;
        float toleranceSquared = tolerance * tolerance;
        for (int i = _mapRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = _mapRoot.GetChild(i);
            if ((child.localPosition - localPosition).sqrMagnitude <= toleranceSquared &&
                child.GetComponentInChildren<T>(true) != null)
                return true;
        }

        return false;
    }

    private Transform FindBlockAt(Vector3 localPosition)
    {
        if (_mapRoot == null)
            return null;

        Vector2 cellStep = GetCellStep(_scale, _spacing);
        float tolerance = Mathf.Min(cellStep.x, cellStep.y) * 0.01f;
        float toleranceSquared = tolerance * tolerance;
        for (int i = _mapRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = _mapRoot.GetChild(i);
            if ((child.localPosition - localPosition).sqrMagnitude <= toleranceSquared)
                return child;
        }

        // Ponytail: linear lookup is enough for editor-sized maps; use a cell dictionary if maps reach thousands of blocks.
        return null;
    }

    private void DrawPreview(Vector3 localPosition, bool erase)
    {
        Color previousColor = Handles.color;
        Matrix4x4 previousMatrix = Handles.matrix;
        Handles.color = erase ? new Color(1f, 0.25f, 0.25f, 0.9f) : new Color(0.2f, 1f, 0.35f, 0.9f);
        Handles.matrix = _mapRoot != null ? _mapRoot.localToWorldMatrix : Matrix4x4.identity;
        Handles.DrawWireCube(localPosition, _scale);
        Handles.matrix = previousMatrix;
        Handles.color = previousColor;
    }

    private GameObject GetRandomSelectedPrefab()
    {
        return _paintType switch
        {
            PaintType.Obstacle => GetRandomSelectedPrefab(_obstaclePrefabs, _selectedObstaclePrefabs),
            PaintType.SpecialMaterial => GetRandomSelectedPrefab(
                _specialMaterialPrefabs, _selectedSpecialMaterialPrefabs),
            PaintType.Overlay => GetRandomSelectedPrefab(_overlayPrefabs, _selectedOverlayPrefabs),
            _ => GetRandomSelectedPrefab(_prefabs, _selectedPrefabs)
        };
    }

    private GameObject GetRandomSelectedPrefab(List<GameObject> prefabs, List<bool> selectedPrefabs)
    {
        int selectedCount = 0;
        for (int i = 0; i < prefabs.Count; i++)
        {
            if (selectedPrefabs[i] && prefabs[i] != null)
                selectedCount++;
        }

        if (selectedCount == 0)
            return null;

        int selectedIndex = _random.Next(selectedCount);
        for (int i = 0; i < prefabs.Count; i++)
        {
            if (!selectedPrefabs[i] || prefabs[i] == null)
                continue;

            if (selectedIndex-- == 0)
                return prefabs[i];
        }

        return null;
    }

    private GameObject GetRandomPrefab(List<GameObject> prefabs)
    {
        int validCount = CountValidPrefabs(prefabs);
        int selectedIndex = _random.Next(validCount);
        for (int i = 0; i < prefabs.Count; i++)
        {
            if (prefabs[i] == null)
                continue;

            if (selectedIndex-- == 0)
                return prefabs[i];
        }

        return null;
    }

    private static int CountValidPrefabs(List<GameObject> prefabs)
    {
        int count = 0;
        for (int i = 0; i < prefabs.Count; i++)
        {
            if (prefabs[i] != null)
                count++;
        }

        return count;
    }

    private static GameObject GetFirstValidPrefab(List<GameObject> prefabs)
    {
        for (int i = 0; i < prefabs.Count; i++)
        {
            if (prefabs[i] != null)
                return prefabs[i];
        }

        return null;
    }

    private GameObject GetGridPrefabForCell(int x, int y)
    {
        if (_gridPrefabs.Count == 12)
            return _gridPrefabs[PositiveModulo(y, 4) * 3 + PositiveModulo(x, 3)];

        return GetRandomPrefab(_gridPrefabs);
    }

    private static int PositiveModulo(int value, int modulus)
    {
        int result = value % modulus;
        return result < 0 ? result + modulus : result;
    }

    private static Vector2 GetCellStep(Vector3 scale, float spacing)
    {
        return new Vector2(scale.x + spacing, scale.y + spacing);
    }

    private static bool IsEraseInput(int mouseButton, bool shift)
    {
        return mouseButton == 1 || shift;
    }

    private static Vector3 GetGridLocalPosition(int x, int y, Vector2 cellStep)
    {
        return new Vector3(x * cellStep.x, y * cellStep.y, 0f);
    }

    private static Vector3 Max(Vector3 value, float minimum)
    {
        return new Vector3(
            Mathf.Max(minimum, value.x),
            Mathf.Max(minimum, value.y),
            Mathf.Max(minimum, value.z));
    }

    private static Vector3 GetPaintLocalPosition(
        Vector3 localPosition, PaintType paintType, float overlayLocalZ)
    {
        if (paintType == PaintType.Overlay)
            localPosition.z = overlayLocalZ;

        return localPosition;
    }

    private static float GetRandomZAngle(System.Random random)
    {
        return (float)(random.NextDouble() * 360d);
    }

    private void EnsureSelectionCount()
    {
        EnsureSelectionCount(_prefabs, _selectedPrefabs);
        EnsureSelectionCount(_obstaclePrefabs, _selectedObstaclePrefabs);
        EnsureSelectionCount(_specialMaterialPrefabs, _selectedSpecialMaterialPrefabs);
        EnsureSelectionCount(_overlayPrefabs, _selectedOverlayPrefabs);
    }

    private static void EnsureSelectionCount(List<GameObject> prefabs, List<bool> selectedPrefabs)
    {
        while (selectedPrefabs.Count < prefabs.Count)
            selectedPrefabs.Add(false);

        while (selectedPrefabs.Count > prefabs.Count)
            selectedPrefabs.RemoveAt(selectedPrefabs.Count - 1);
    }

    private void CreateLevel()
    {
        if (!TryGetLevelAssetPath(out string levelName, out string assetPath))
            return;

        GameObject root = new GameObject(levelName);
        Undo.RegisterCreatedObjectUndo(root, "Create Level");
        _mapRoot = root.transform;
        Selection.activeGameObject = root;
        SaveLevelPrefab(levelName, assetPath, true);
    }

    private void LoadLevel()
    {
        string levelName = BuildLevelName(_loadLevelNumber.ToString());
        string assetPath = BuildLevelAssetPath(levelName);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (prefab == null)
        {
            EditorUtility.DisplayDialog("Map Painter", $"Level prefab not found:\n{assetPath}", "OK");
            return;
        }

        _levelName = levelName;
        if (!LoadLevelSettings(levelName))
        {
            RestorePrefabListsFromLevelPrefab(prefab, assetPath);
            SaveLevelSettings(levelName);
        }

        if (AssetDatabase.OpenAsset(prefab))
            EditorApplication.delayCall += BindCurrentPrefabRoot;
    }

    private void SaveLevelPrefab()
    {
        PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
        if (prefabStage != null && _mapRoot != null && prefabStage.prefabContentsRoot == _mapRoot.gameObject)
        {
            int removedDuplicates = RemoveDuplicateLevelBlocks(_mapRoot);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
                prefabStage.prefabContentsRoot, prefabStage.assetPath, out bool success);
            if (success && prefab != null)
            {
                prefabStage.ClearDirtiness();
                SaveLevelSettings(Path.GetFileNameWithoutExtension(prefabStage.assetPath));
                Debug.Log(
                    $"Saved level prefab: {prefabStage.assetPath}. Removed {removedDuplicates} duplicate blocks.",
                    prefab);
            }
            return;
        }

        if (_mapRoot == null)
        {
            EditorUtility.DisplayDialog("Map Painter", "Create or assign a Map Root first.", "OK");
            return;
        }

        if (!TryGetLevelAssetPath(out string levelName, out string assetPath))
            return;

        SaveLevelPrefab(levelName, assetPath, false);
    }

    private bool TryGetLevelAssetPath(out string levelName, out string assetPath)
    {
        levelName = BuildLevelName(_levelName);
        assetPath = string.Empty;
        if (string.IsNullOrEmpty(levelName) || HasInvalidFileNameCharacter(levelName))
        {
            EditorUtility.DisplayDialog("Map Painter", "Level name is empty or contains invalid characters.", "OK");
            return false;
        }

        if (!AssetDatabase.IsValidFolder(LevelFolder))
            AssetDatabase.CreateFolder("Assets/_Project/Resources", "Level");

        assetPath = BuildLevelAssetPath(levelName);
        if (AssetDatabase.LoadAssetAtPath<GameObject>(assetPath) != null &&
            !EditorUtility.DisplayDialog("Overwrite Level Prefab?", $"{assetPath} already exists.", "Overwrite", "Cancel"))
        {
            return false;
        }

        return true;
    }

    private void SaveLevelPrefab(string levelName, string assetPath, bool openAfterSave)
    {
        Undo.RecordObject(_mapRoot.gameObject, "Name Level Root");
        _mapRoot.name = levelName;
        int removedDuplicates = RemoveDuplicateLevelBlocks(_mapRoot);
        GameObject prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(
            _mapRoot.gameObject, assetPath, InteractionMode.UserAction, out bool success);
        if (!success || prefab == null)
            return;

        _levelName = levelName;
        SaveLevelSettings(levelName);
        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);
        Debug.Log($"Saved level prefab: {assetPath}. Removed {removedDuplicates} duplicate blocks.", prefab);

        if (openAfterSave && AssetDatabase.OpenAsset(prefab))
            EditorApplication.delayCall += BindCurrentPrefabRoot;
    }

    private static int RemoveDuplicateLevelBlocks(Transform root)
    {
        List<TypeBlockMap> duplicates = new();
        CollectDuplicateLevelBlocks(root, duplicates);
        for (int i = 0; i < duplicates.Count; i++)
            Undo.DestroyObjectImmediate(duplicates[i].gameObject);

        return duplicates.Count;
    }

    private static void CollectDuplicateLevelBlocks(Transform root, List<TypeBlockMap> duplicates)
    {
        TypeBlockMap[] blocks = root.GetComponentsInChildren<TypeBlockMap>(true);
        if (blocks.Length < 2)
            return;

        float cellSize = root.InverseTransformVector(
            blocks[0].transform.TransformVector(Vector3.right * blocks[0].CellSize)).magnitude;
        if (cellSize <= 0.0001f)
            return;

        Vector2 min = new(float.MaxValue, float.MaxValue);
        for (int i = 0; i < blocks.Length; i++)
        {
            Vector3 position = root.InverseTransformPoint(blocks[i].transform.position);
            min.x = Mathf.Min(min.x, position.x);
            min.y = Mathf.Min(min.y, position.y);
        }

        BlockWater[] waterMarkers = root.GetComponentsInChildren<BlockWater>(true);
        for (int i = 0; i < waterMarkers.Length; i++)
        {
            Vector3 position = root.InverseTransformPoint(waterMarkers[i].transform.position);
            min.x = Mathf.Min(min.x, position.x);
            min.y = Mathf.Min(min.y, position.y);
        }

        HashSet<Vector2Int> occupiedCells = new(blocks.Length);
        for (int i = 0; i < blocks.Length; i++)
        {
            Vector3 position = root.InverseTransformPoint(blocks[i].transform.position);
            Vector2Int cell = new(
                Mathf.RoundToInt((position.x - min.x) / cellSize),
                Mathf.RoundToInt((position.y - min.y) / cellSize));
            if (!occupiedCells.Add(cell))
                duplicates.Add(blocks[i]);
        }
    }

    private void BindCurrentPrefabRoot()
    {
        PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
        if (prefabStage == null)
            return;

        _mapRoot = prefabStage.prefabContentsRoot.transform;
        ApplyPreviewColors(_mapRoot);
        Selection.activeTransform = _mapRoot;
        Repaint();
    }

    private static void ApplyPreviewColors(Transform root)
    {
        PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
        bool preserveCleanStage = prefabStage != null &&
                                  prefabStage.prefabContentsRoot.transform == root &&
                                  !prefabStage.scene.isDirty;

        MaterialPropertyBlock properties = new();
        TypeBlockMap[] blocks = root.GetComponentsInChildren<TypeBlockMap>(true);
        for (int i = 0; i < blocks.Length; i++)
            ApplyPreviewColor(blocks[i], properties);

        LevelDecoration[] decorations = root.GetComponentsInChildren<LevelDecoration>(true);
        for (int i = 0; i < decorations.Length; i++)
            ApplyPreviewColor(decorations[i], properties);

        if (preserveCleanStage)
            prefabStage.ClearDirtiness();

        SceneView.RepaintAll();
    }

    private void ApplySelectedPreviewColor()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null || _mapRoot == null || selected.transform == _mapRoot ||
            !selected.transform.IsChildOf(_mapRoot))
            return;

        PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
        bool preserveCleanStage = prefabStage != null && !prefabStage.scene.isDirty;
        TypeBlockMap blockMap = selected.GetComponentInParent<TypeBlockMap>();
        if (blockMap != null)
            ApplyPreviewColor(blockMap);

        LevelDecoration decoration = selected.GetComponentInParent<LevelDecoration>();
        if (decoration != null)
            ApplyPreviewColor(decoration);

        if (preserveCleanStage)
            prefabStage.ClearDirtiness();
    }

    private void ApplyPreviewColor(GameObject instance)
    {
        TypeBlockMap blockMap = instance.GetComponentInChildren<TypeBlockMap>(true);
        if (blockMap != null)
            ApplyPreviewColor(blockMap);

        LevelDecoration decoration = instance.GetComponentInChildren<LevelDecoration>(true);
        if (decoration != null)
            ApplyPreviewColor(decoration);
    }

    private void ApplyPreviewColor(TypeBlockMap blockMap)
    {
        ApplyPreviewColor(blockMap, _previewProperties);
    }

    private void ApplyPreviewColor(LevelDecoration decoration)
    {
        ApplyPreviewColor(decoration, _previewProperties);
    }

    private static void ApplyPreviewColor(TypeBlockMap blockMap, MaterialPropertyBlock properties)
    {
        ApplyPreviewColor(blockMap.GetComponent<MeshRenderer>(), blockMap.MapColor, false, properties);
    }

    private static void ApplyPreviewColor(LevelDecoration decoration, MaterialPropertyBlock properties)
    {
        MeshFilter meshFilter = decoration.GetComponent<MeshFilter>();
        Mesh mesh = meshFilter != null ? meshFilter.sharedMesh : null;
        bool useVertexColor = mesh != null && mesh.colors32.Length == mesh.vertexCount;
        ApplyPreviewColor(decoration.GetComponent<MeshRenderer>(), decoration.Tint, useVertexColor, properties);
    }

    private void ApplyPreviewColor(MeshRenderer renderer, Color color, bool useVertexColor)
    {
        ApplyPreviewColor(renderer, color, useVertexColor, _previewProperties);
    }

    private static void ApplyPreviewColor(
        MeshRenderer renderer, Color color, bool useVertexColor, MaterialPropertyBlock properties)
    {
        if (renderer == null || renderer.sharedMaterial == null ||
            !renderer.sharedMaterial.HasProperty(ColorPropertyId))
            return;

        properties.Clear();
        renderer.GetPropertyBlock(properties);
        properties.SetColor(ColorPropertyId, color);
        properties.SetFloat(UseVertexColorPropertyId, useVertexColor ? 1f : 0f);
        renderer.SetPropertyBlock(properties);
    }

    private static string BuildLevelName(string value)
    {
        string trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return string.Empty;

        return trimmed.StartsWith("level_", StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : $"level_{trimmed}";
    }

    private static string BuildLevelAssetPath(string levelName)
    {
        return $"{LevelFolder}/{levelName}.prefab";
    }

    private static string BuildLevelSettingsPath(string levelName)
    {
        return $"{LevelSettingsFolder}/{levelName}.asset";
    }

    private void SaveLevelSettings(string levelName)
    {
        if (!AssetDatabase.IsValidFolder(LevelSettingsFolder))
            AssetDatabase.CreateFolder("Assets/_Project/Editor", "MapPainterData");

        string assetPath = BuildLevelSettingsPath(levelName);
        MapPainterLevelSettings settings = AssetDatabase.LoadAssetAtPath<MapPainterLevelSettings>(assetPath);
        if (settings == null)
        {
            settings = CreateInstance<MapPainterLevelSettings>();
            AssetDatabase.CreateAsset(settings, assetPath);
        }

        settings.PaintPrefabs.Clear();
        settings.PaintPrefabs.AddRange(_prefabs);
        settings.SelectedPaintPrefabs.Clear();
        settings.SelectedPaintPrefabs.AddRange(_selectedPrefabs);
        settings.ObstaclePrefabs.Clear();
        settings.ObstaclePrefabs.AddRange(_obstaclePrefabs);
        settings.SelectedObstaclePrefabs.Clear();
        settings.SelectedObstaclePrefabs.AddRange(_selectedObstaclePrefabs);
        settings.SpecialMaterialPrefabs.Clear();
        settings.SpecialMaterialPrefabs.AddRange(_specialMaterialPrefabs);
        settings.SelectedSpecialMaterialPrefabs.Clear();
        settings.SelectedSpecialMaterialPrefabs.AddRange(_selectedSpecialMaterialPrefabs);
        settings.OverlayPrefabs.Clear();
        settings.OverlayPrefabs.AddRange(_overlayPrefabs);
        settings.SelectedOverlayPrefabs.Clear();
        settings.SelectedOverlayPrefabs.AddRange(_selectedOverlayPrefabs);
        settings.OverlayScaleMultiplier = _overlayScaleMultiplier;
        settings.OverlayLocalZ = _overlayLocalZ;
        settings.HasOverlayLocalZ = true;
        settings.GridPrefabs.Clear();
        settings.GridPrefabs.AddRange(_gridPrefabs);
        settings.GridColorLayers.Clear();
        for (int i = 0; i < _gridColorLayers.Count; i++)
            settings.GridColorLayers.Add(new MapPainterGridColorLayer(_gridColorLayers[i]));
        settings.PaintType = (int)_paintType;
        settings.Scale = _scale;
        settings.HasScale = true;
        settings.Spacing = _spacing;
        settings.GridWidth = _gridWidth;
        settings.GridHeight = _gridHeight;
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
    }

    private bool LoadLevelSettings(string levelName)
    {
        MapPainterLevelSettings settings = AssetDatabase.LoadAssetAtPath<MapPainterLevelSettings>(
            BuildLevelSettingsPath(levelName));
        if (settings == null)
            return false;

        _prefabs.Clear();
        _prefabs.AddRange(settings.PaintPrefabs);
        _selectedPrefabs.Clear();
        _selectedPrefabs.AddRange(settings.SelectedPaintPrefabs);
        _obstaclePrefabs.Clear();
        _obstaclePrefabs.AddRange(settings.ObstaclePrefabs);
        _selectedObstaclePrefabs.Clear();
        _selectedObstaclePrefabs.AddRange(settings.SelectedObstaclePrefabs);
        _specialMaterialPrefabs.Clear();
        _specialMaterialPrefabs.AddRange(settings.SpecialMaterialPrefabs);
        _selectedSpecialMaterialPrefabs.Clear();
        _selectedSpecialMaterialPrefabs.AddRange(settings.SelectedSpecialMaterialPrefabs);
        _overlayPrefabs.Clear();
        _overlayPrefabs.AddRange(settings.OverlayPrefabs);
        _selectedOverlayPrefabs.Clear();
        _selectedOverlayPrefabs.AddRange(settings.SelectedOverlayPrefabs);
        _overlayScaleMultiplier = Mathf.Max(0.01f, settings.OverlayScaleMultiplier);
        _overlayLocalZ = settings.HasOverlayLocalZ ? settings.OverlayLocalZ : DefaultOverlayLocalZ;
        _gridPrefabs.Clear();
        _gridPrefabs.AddRange(settings.GridPrefabs);
        _gridColorLayers.Clear();
        for (int i = 0; i < settings.GridColorLayers.Count; i++)
            _gridColorLayers.Add(new MapPainterGridColorLayer(settings.GridColorLayers[i]));
        _paintType = (PaintType)settings.PaintType;
        _scale = settings.HasScale ? settings.Scale : Vector3.one * settings.PrefabSize;
        _spacing = settings.Spacing;
        _gridWidth = settings.GridWidth;
        _gridHeight = settings.GridHeight;
        EnsureSelectionCount();
        Repaint();
        return true;
    }

    private void RestorePrefabListsFromLevelPrefab(GameObject levelPrefab, string levelAssetPath)
    {
        _prefabs.Clear();
        _selectedPrefabs.Clear();
        _obstaclePrefabs.Clear();
        _selectedObstaclePrefabs.Clear();
        _specialMaterialPrefabs.Clear();
        _selectedSpecialMaterialPrefabs.Clear();
        _overlayPrefabs.Clear();
        _selectedOverlayPrefabs.Clear();
        _gridPrefabs.Clear();

        Transform[] children = levelPrefab.GetComponentsInChildren<Transform>(true);
        for (int i = 1; i < children.Length; i++)
        {
            string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(children[i].gameObject);
            if (string.IsNullOrEmpty(prefabPath) || prefabPath == levelAssetPath)
                continue;

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null || _prefabs.Contains(prefab))
                continue;

            _prefabs.Add(prefab);
            _selectedPrefabs.Add(true);
            _gridPrefabs.Add(prefab);
        }
    }

    private static bool HasInvalidFileNameCharacter(string value)
    {
        char[] invalidCharacters = Path.GetInvalidFileNameChars();
        for (int i = 0; i < invalidCharacters.Length; i++)
        {
            if (value.IndexOf(invalidCharacters[i]) >= 0)
                return true;
        }

        return false;
    }

    private void BeginStroke(string name)
    {
        EndStroke();
        Undo.IncrementCurrentGroup();
        _undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(name);
    }

    private void EndStroke()
    {
        _lastPaintedCell = new Vector2Int(int.MinValue, int.MinValue);
        if (_undoGroup < 0)
            return;

        Undo.CollapseUndoOperations(_undoGroup);
        _undoGroup = -1;
    }

    [MenuItem("Tools/Map Painter Self Check")]
    private static void RunSelfCheck()
    {
        Vector3 scale = new(1.2f, 0.8f, 0.6f);
        Vector2 cellStep = GetCellStep(scale, 0.3f);
        Vector2Int cell = new(
            Mathf.RoundToInt(1.79f / cellStep.x),
            Mathf.RoundToInt(-1.81f / cellStep.y));
        Debug.Assert(cell == new Vector2Int(1, -2), "Map Painter grid snapping failed.");
        Debug.Assert(BuildLevelName("2") == "level_2", "Map Painter level naming failed.");
        Debug.Assert(BuildLevelName("level_3") == "level_3", "Map Painter duplicated the level prefix.");
        Debug.Assert(BuildLevelAssetPath("level_4") == "Assets/_Project/Resources/Level/level_4.prefab",
            "Map Painter level loading path failed.");
        Debug.Assert(BuildLevelSettingsPath("level_4") == "Assets/_Project/Editor/MapPainterData/level_4.asset",
            "Map Painter level settings path failed.");
        Debug.Assert(cellStep == new Vector2(1.5f, 1.1f),
            "Map Painter prefab spacing failed.");
        Debug.Assert(IsEraseInput(1, false) && IsEraseInput(0, true) && !IsEraseInput(0, false),
            "Map Painter erase input failed.");
        Debug.Assert(GetGridLocalPosition(2, 3, cellStep) == new Vector3(3f, 3.3f, 0f),
            "Map Painter grid generation position failed.");
        Debug.Assert(GetPaintLocalPosition(Vector3.zero, PaintType.Overlay, DefaultOverlayLocalZ).z ==
                     DefaultOverlayLocalZ,
            "Map Painter overlay depth failed.");
        Debug.Assert(GetPaintLocalPosition(Vector3.zero, PaintType.Overlay, 0.75f).z == 0.75f,
            "Map Painter custom overlay depth failed.");
        System.Random rotationRandom = new(1234);
        Debug.Assert(!Mathf.Approximately(
                GetRandomZAngle(rotationRandom), GetRandomZAngle(rotationRandom)),
            "Map Painter overlay random rotation failed.");
        Debug.Assert(PositiveModulo(-1, 3) == 2 && PositiveModulo(4, 3) == 1,
            "Map Painter mosaic indexing failed.");
        List<MapPainterGridColorLayer> colorLayers = new()
        {
            new MapPainterGridColorLayer(0, 1, Color.yellow),
            new MapPainterGridColorLayer(2, 4, Color.red)
        };
        Debug.Assert(TryGetGridLayerColor(colorLayers, 5, 4, out Color topColor) &&
                     topColor == Color.yellow &&
                     TryGetGridLayerColor(colorLayers, 5, 0, out Color bottomColor) &&
                     bottomColor == Color.red &&
                     !TryGetGridLayerColor(colorLayers, 5, -1, out _),
            "Map Painter grid color layer ranges failed.");

        GameObject duplicateRoot = new("MapPainterDuplicateSelfCheck");
        Material previewMaterial = null;
        try
        {
            GameObject first = new("First");
            first.transform.SetParent(duplicateRoot.transform, false);
            first.AddComponent<TypeBlockMap>();
            GameObject duplicate = new("Duplicate");
            duplicate.transform.SetParent(duplicateRoot.transform, false);
            duplicate.AddComponent<TypeBlockMap>();
            List<TypeBlockMap> duplicateBlocks = new();
            CollectDuplicateLevelBlocks(duplicateRoot.transform, duplicateBlocks);
            Debug.Assert(duplicateBlocks.Count == 1 && duplicateBlocks[0].gameObject == duplicate,
                "Map Painter duplicate block detection failed.");

            List<GameObject> droppedPrefabs = new() { null };
            List<bool> droppedSelections = new() { false };
            Debug.Assert(TryAddPrefab(first, droppedPrefabs, droppedSelections) &&
                         TryAddPrefab(duplicate, droppedPrefabs, droppedSelections) &&
                         !TryAddPrefab(first, droppedPrefabs, droppedSelections) &&
                         droppedPrefabs.Count == 2 && droppedSelections[0] && droppedSelections[1],
                "Map Painter multi-prefab drop failed.");

            MeshRenderer renderer = first.GetComponent<MeshRenderer>();
            previewMaterial = new Material(Shader.Find("BlockCrusher/VoxelExactColor"));
            renderer.sharedMaterial = previewMaterial;
            Color32 expectedColor = new(31, 79, 127, 255);
            MaterialPropertyBlock properties = new();
            ApplyPreviewColor(renderer, expectedColor, false, properties);
            renderer.GetPropertyBlock(properties);
            Debug.Assert(properties.GetColor(ColorPropertyId) == (Color)expectedColor &&
                         properties.GetFloat(UseVertexColorPropertyId) == 0f,
                "Map Painter preview color failed.");
        }
        finally
        {
            DestroyImmediate(previewMaterial);
            DestroyImmediate(duplicateRoot);
        }

        Debug.Log("Map Painter self check passed.");
    }
}
