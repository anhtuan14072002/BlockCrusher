using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Serialization;

public sealed class MapPainterWindow : EditorWindow
{
    private enum PaintType
    {
        Block,
        Obstacle,
        SpecialMaterial
    }

    private const float DefaultPrefabSize = 1f;
    private const string LevelFolder = "Assets/_Project/Resources/Level";
    private const string LevelSettingsFolder = "Assets/_Project/Editor/MapPainterData";
    private static readonly string[] EditModeLabels = { "None", "Edit" };

    [SerializeField] private List<GameObject> _prefabs = new();
    [SerializeField] private List<bool> _selectedPrefabs = new();
    [SerializeField] private List<GameObject> _obstaclePrefabs = new();
    [SerializeField] private List<bool> _selectedObstaclePrefabs = new();
    [SerializeField] private List<GameObject> _specialMaterialPrefabs = new();
    [SerializeField] private List<bool> _selectedSpecialMaterialPrefabs = new();
    [SerializeField] private List<GameObject> _gridPrefabs = new();
    [SerializeField] private Transform _mapRoot;
    [FormerlySerializedAs("_gridSize")]
    [SerializeField] private float _prefabSize = DefaultPrefabSize;
    [SerializeField] private float _spacing;
    [SerializeField] private int _gridWidth = 10;
    [SerializeField] private int _gridHeight = 10;
    [SerializeField] private string _levelName = "level_1";
    [SerializeField] private int _loadLevelNumber = 1;
    [SerializeField] private bool _paintEnabled = true;
    [SerializeField] private PaintType _paintType;

    private readonly System.Random _random = new();
    private Vector2 _scrollPosition;
    private Vector2Int _lastPaintedCell = new(int.MinValue, int.MinValue);
    private int _undoGroup = -1;

    [MenuItem("Tools/Map Painter")]
    private static void Open()
    {
        GetWindow<MapPainterWindow>("Map Painter");
    }

    private void OnEnable()
    {
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

        _prefabSize = Mathf.Max(0.01f, EditorGUILayout.FloatField("Prefab Size", _prefabSize));
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
            "Special Material Prefabs", "Add Special Material Prefab Slot", PaintType.SpecialMaterial,
            _specialMaterialPrefabs, _selectedSpecialMaterialPrefabs);
        EditorGUILayout.EndScrollView();
    }

    private void DrawAdditionalPrefabList(
        string title, string addButtonLabel, PaintType paintType,
        List<GameObject> prefabs, List<bool> selectedPrefabs)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
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
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (!_paintEnabled || _mapRoot == null || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        Event current = Event.current;
        if (current.alt)
            return;

        if (!TryGetCell(current.mousePosition, out Vector2Int cell, out Vector3 localPosition))
            return;

        bool erase = IsEraseInput(current.button, current.shift);
        DrawPreview(localPosition, erase);

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
        float cellStep = GetCellStep(_prefabSize, _spacing);
        cell = new Vector2Int(
            Mathf.RoundToInt(rawLocal.x / cellStep),
            Mathf.RoundToInt(rawLocal.y / cellStep));
        localPosition = new Vector3(cell.x * cellStep, cell.y * cellStep, 0f);
        return true;
    }

    private void ApplyCell(Vector2Int cell, Vector3 localPosition, bool erase)
    {
        if (cell == _lastPaintedCell)
            return;

        _lastPaintedCell = cell;
        if (erase)
        {
            EraseAt(localPosition);
            return;
        }

        GameObject prefab = GetRandomSelectedPrefab();
        if (prefab == null || (_paintType == PaintType.Block && HasBlockAt(localPosition)))
            return;

        CreateBlock(prefab, localPosition);
    }

    private void CreateBlock(GameObject prefab, Vector3 localPosition)
    {
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, _mapRoot);
        Undo.RegisterCreatedObjectUndo(instance, "Paint Map Block");
        instance.transform.localPosition = localPosition;
        instance.transform.localScale = Vector3.one * _prefabSize;
    }

    private void GenerateGrid()
    {
        if (_mapRoot == null || CountValidPrefabs(_gridPrefabs) == 0)
        {
            EditorUtility.DisplayDialog("Map Painter", "Assign a Map Root and at least one Grid Prefab.", "OK");
            return;
        }

        HashSet<Vector2Int> occupiedCells = new(_mapRoot.childCount);
        float cellStep = GetCellStep(_prefabSize, _spacing);
        for (int i = 0; i < _mapRoot.childCount; i++)
        {
            Vector3 position = _mapRoot.GetChild(i).localPosition;
            occupiedCells.Add(new Vector2Int(
                Mathf.RoundToInt(position.x / cellStep),
                Mathf.RoundToInt(position.y / cellStep)));
        }

        BeginStroke("Generate Map Grid");
        for (int y = 0; y < _gridHeight; y++)
        {
            for (int x = 0; x < _gridWidth; x++)
            {
                Vector2Int cell = new(x, y);
                if (!occupiedCells.Add(cell))
                    continue;

                CreateBlock(GetRandomPrefab(_gridPrefabs), GetGridLocalPosition(x, y, cellStep));
            }
        }
        EndStroke();
        SceneView.RepaintAll();
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

    private Transform FindBlockAt(Vector3 localPosition)
    {
        if (_mapRoot == null)
            return null;

        float tolerance = GetCellStep(_prefabSize, _spacing) * 0.01f;
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
        Handles.DrawWireCube(localPosition, new Vector3(_prefabSize, _prefabSize, _prefabSize));
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

    private static float GetCellStep(float prefabSize, float spacing)
    {
        return prefabSize + spacing;
    }

    private static bool IsEraseInput(int mouseButton, bool shift)
    {
        return mouseButton == 1 || shift;
    }

    private static Vector3 GetGridLocalPosition(int x, int y, float cellStep)
    {
        return new Vector3(x * cellStep, y * cellStep, 0f);
    }

    private void EnsureSelectionCount()
    {
        EnsureSelectionCount(_prefabs, _selectedPrefabs);
        EnsureSelectionCount(_obstaclePrefabs, _selectedObstaclePrefabs);
        EnsureSelectionCount(_specialMaterialPrefabs, _selectedSpecialMaterialPrefabs);
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
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
                prefabStage.prefabContentsRoot, prefabStage.assetPath, out bool success);
            if (success && prefab != null)
            {
                prefabStage.ClearDirtiness();
                SaveLevelSettings(Path.GetFileNameWithoutExtension(prefabStage.assetPath));
                Debug.Log($"Saved level prefab: {prefabStage.assetPath}", prefab);
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
        GameObject prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(
            _mapRoot.gameObject, assetPath, InteractionMode.UserAction, out bool success);
        if (!success || prefab == null)
            return;

        _levelName = levelName;
        SaveLevelSettings(levelName);
        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);
        Debug.Log($"Saved level prefab: {assetPath}", prefab);

        if (openAfterSave && AssetDatabase.OpenAsset(prefab))
            EditorApplication.delayCall += BindCurrentPrefabRoot;
    }

    private void BindCurrentPrefabRoot()
    {
        PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
        if (prefabStage == null)
            return;

        _mapRoot = prefabStage.prefabContentsRoot.transform;
        Selection.activeTransform = _mapRoot;
        Repaint();
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
        settings.GridPrefabs.Clear();
        settings.GridPrefabs.AddRange(_gridPrefabs);
        settings.PaintType = (int)_paintType;
        settings.PrefabSize = _prefabSize;
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
        _gridPrefabs.Clear();
        _gridPrefabs.AddRange(settings.GridPrefabs);
        _paintType = (PaintType)settings.PaintType;
        _prefabSize = settings.PrefabSize;
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
        float prefabSize = DefaultPrefabSize;
        Vector2Int cell = new(
            Mathf.RoundToInt(1.79f / prefabSize),
            Mathf.RoundToInt(-1.81f / prefabSize));
        Debug.Assert(cell == new Vector2Int(1, -2), "Map Painter grid snapping failed.");
        Debug.Assert(BuildLevelName("2") == "level_2", "Map Painter level naming failed.");
        Debug.Assert(BuildLevelName("level_3") == "level_3", "Map Painter duplicated the level prefix.");
        Debug.Assert(BuildLevelAssetPath("level_4") == "Assets/_Project/Resources/Level/level_4.prefab",
            "Map Painter level loading path failed.");
        Debug.Assert(BuildLevelSettingsPath("level_4") == "Assets/_Project/Editor/MapPainterData/level_4.asset",
            "Map Painter level settings path failed.");
        Debug.Assert(Mathf.Approximately(GetCellStep(prefabSize, 0.3f), 1.5f),
            "Map Painter prefab spacing failed.");
        Debug.Assert(IsEraseInput(1, false) && IsEraseInput(0, true) && !IsEraseInput(0, false),
            "Map Painter erase input failed.");
        Debug.Assert(GetGridLocalPosition(2, 3, prefabSize) == new Vector3(2.4f, 3.6f, 0f),
            "Map Painter grid generation position failed.");
        Debug.Log("Map Painter self check passed.");
    }
}
