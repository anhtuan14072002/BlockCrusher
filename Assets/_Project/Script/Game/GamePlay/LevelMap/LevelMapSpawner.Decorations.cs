using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public sealed partial class LevelMapSpawner
{
    private readonly List<DecorationRuntime> _levelDecorations = new(256);
    private List<int>[] _decorationChunksByCell;
    private List<int>[] _decorationsByAnchorCell;

    private sealed class DecorationRuntime
    {
        public Vector3[] Vertices;
        public Vector2[] Uvs;
        public Color32[] Colors;
        public int[] Triangles;
        public Matrix4x4 Matrix;
        public Color32 Tint;
        public Color32 ReleasedColor;
        public int[] TriangleCells;
        public ushort ReleasedTypeIndex;
    }

    private void CreateDecorations(LevelDecoration[] decorations)
    {
        DisposeDecorations();
        if (decorations.Length == 0 || _chunks.Count == 0)
            return;

        List<int>[] chunkDecorations = new List<int>[_chunks.Count];
        _decorationChunksByCell = new List<int>[_cellSolid.Length];
        _decorationsByAnchorCell = new List<int>[_cellSolid.Length];
        Matrix4x4 worldToGrid = _runtimeParent.worldToLocalMatrix;
        for (int i = 0; i < decorations.Length; i++)
        {
            LevelDecoration decoration = decorations[i];
            MeshFilter filter = decoration.GetComponent<MeshFilter>();
            Mesh sourceMesh = filter.sharedMesh;
            if (sourceMesh == null)
                continue;

            Vector3 localPosition = _runtimeParent.InverseTransformPoint(decoration.transform.position);
            int cellX = Mathf.RoundToInt((localPosition.x - _offset.x) / _cellSize);
            int cellY = Mathf.RoundToInt((localPosition.y - _offset.y) / _cellSize);
            if ((uint)cellX >= (uint)_gridWidth || (uint)cellY >= (uint)_gridHeight)
                continue;

            int cellIndex = cellY * _gridWidth + cellX;
            if (_cellSolid[cellIndex] == 0)
            {
                cellIndex = FindNearestDecorationCell(localPosition, cellX, cellY);
                if (cellIndex < 0)
                    continue;
                cellX = cellIndex % _gridWidth;
                cellY = cellIndex / _gridWidth;
            }

            int chunkIndex = cellY / _chunkSize * _chunkColumns + cellX / _chunkSize;
            Matrix4x4 matrix = worldToGrid * decoration.transform.localToWorldMatrix;
            Vector3[] vertices = sourceMesh.vertices;
            int[] triangles = sourceMesh.triangles;
            int[] triangleCells = new int[triangles.Length / 3];
            HashSet<int> coveredCells = new HashSet<int>();
            for (int triangle = 0; triangle < triangleCells.Length; triangle++)
            {
                int index = triangle * 3;
                Vector3 center = (matrix.MultiplyPoint3x4(vertices[triangles[index]]) +
                                  matrix.MultiplyPoint3x4(vertices[triangles[index + 1]]) +
                                  matrix.MultiplyPoint3x4(vertices[triangles[index + 2]])) / 3f;
                int triangleCell = FindNearestDecorationCell(center,
                    Mathf.RoundToInt((center.x - _offset.x) / _cellSize),
                    Mathf.RoundToInt((center.y - _offset.y) / _cellSize));
                triangleCells[triangle] = triangleCell >= 0 ? triangleCell : cellIndex;
                coveredCells.Add(triangleCells[triangle]);
            }

            int releasedTypeIndex = GetOrCreateReleasedBlockType(decoration);
            DecorationRuntime runtime = new DecorationRuntime
            {
                Vertices = vertices,
                Uvs = sourceMesh.uv,
                Colors = sourceMesh.colors32,
                Triangles = triangles,
                Matrix = matrix,
                Tint = decoration.Tint,
                ReleasedColor = decoration.ReleasedColor,
                TriangleCells = triangleCells,
                ReleasedTypeIndex = releasedTypeIndex >= 0 ? (ushort)releasedTypeIndex : ushort.MaxValue
            };
            int decorationIndex = _levelDecorations.Count;
            _levelDecorations.Add(runtime);
            chunkDecorations[chunkIndex] ??= new List<int>();
            chunkDecorations[chunkIndex].Add(decorationIndex);

            foreach (int coveredCell in coveredCells)
            {
                _decorationChunksByCell[coveredCell] ??= new List<int>(1);
                if (!_decorationChunksByCell[coveredCell].Contains(chunkIndex))
                    _decorationChunksByCell[coveredCell].Add(chunkIndex);
            }

            if (runtime.ReleasedTypeIndex != ushort.MaxValue)
            {
                _decorationsByAnchorCell[cellIndex] ??= new List<int>(1);
                _decorationsByAnchorCell[cellIndex].Add(decorationIndex);
            }
        }

        for (int chunkIndex = 0; chunkIndex < _chunks.Count; chunkIndex++)
        {
            List<int> decorationIndices = chunkDecorations[chunkIndex];
            if (decorationIndices == null)
                continue;

            ChunkRuntime chunk = _chunks[chunkIndex];
            int vertexCapacity = decorationIndices.Count * 24;
            chunk.DecorationIndices = decorationIndices.ToArray();
            chunk.DecorationVertices = new List<Vector3>(vertexCapacity);
            chunk.DecorationColors = new List<Color32>(vertexCapacity);
            chunk.DecorationUvs = new List<Vector2>(vertexCapacity);
            chunk.DecorationTriangles = new List<int>(decorationIndices.Count * 36);
            chunk.DecorationMesh = new Mesh { name = $"LevelDecorations_{chunk.StartX}_{chunk.StartY}" };
            chunk.DecorationMesh.MarkDynamic();
            chunk.DecorationObject = new GameObject(chunk.DecorationMesh.name);
            chunk.DecorationObject.transform.SetParent(_runtimeParent, false);
            chunk.DecorationObject.transform.localPosition = Vector3.back * _chunkColliderDepth;
            chunk.DecorationObject.AddComponent<MeshFilter>().sharedMesh = chunk.DecorationMesh;
            chunk.DecorationRenderer = chunk.DecorationObject.AddComponent<MeshRenderer>();
            chunk.DecorationRenderer.sharedMaterial = _chunkMaterial;
            chunk.DecorationRenderer.shadowCastingMode = ShadowCastingMode.Off;
            chunk.DecorationRenderer.receiveShadows = false;
            ApplyChunkDecorations(ref chunk);
            _chunks[chunkIndex] = chunk;
        }
    }

    private int FindNearestDecorationCell(Vector3 localPosition, int centerX, int centerY)
    {
        int nearestCell = -1;
        float nearestDistance = float.MaxValue;
        for (int y = Mathf.Max(0, centerY - 2); y <= Mathf.Min(_gridHeight - 1, centerY + 2); y++)
        {
            for (int x = Mathf.Max(0, centerX - 2); x <= Mathf.Min(_gridWidth - 1, centerX + 2); x++)
            {
                int cell = y * _gridWidth + x;
                if (_cellSolid[cell] == 0)
                    continue;
                Vector3 delta = GetCellLocalPosition(x, y) - localPosition;
                float distance = delta.x * delta.x + delta.y * delta.y;
                if (distance >= nearestDistance)
                    continue;
                nearestDistance = distance;
                nearestCell = cell;
            }
        }
        return nearestCell;
    }

    private void ApplyChunkDecorations(ref ChunkRuntime chunk)
    {
        if (chunk.DecorationMesh == null || chunk.DecorationIndices == null)
            return;

        List<Vector3> vertices = chunk.DecorationVertices;
        List<Color32> colors = chunk.DecorationColors;
        List<Vector2> uvs = chunk.DecorationUvs;
        List<int> triangles = chunk.DecorationTriangles;
        vertices.Clear();
        colors.Clear();
        uvs.Clear();
        triangles.Clear();

        for (int i = 0; i < chunk.DecorationIndices.Length; i++)
        {
            DecorationRuntime decoration = _levelDecorations[chunk.DecorationIndices[i]];
            int vertexOffset = vertices.Count;
            for (int vertexIndex = 0; vertexIndex < decoration.Vertices.Length; vertexIndex++)
            {
                vertices.Add(decoration.Matrix.MultiplyPoint3x4(decoration.Vertices[vertexIndex]));
                colors.Add(decoration.Colors.Length == decoration.Vertices.Length
                    ? MultiplyColor(decoration.Tint, decoration.Colors[vertexIndex])
                    : decoration.Tint);
                uvs.Add(decoration.Uvs.Length == decoration.Vertices.Length
                    ? decoration.Uvs[vertexIndex]
                    : Vector2.zero);
            }
            for (int triangle = 0; triangle < decoration.TriangleCells.Length; triangle++)
            {
                if (_cellSolid[decoration.TriangleCells[triangle]] == 0)
                    continue;

                int triangleIndex = triangle * 3;
                triangles.Add(vertexOffset + decoration.Triangles[triangleIndex]);
                triangles.Add(vertexOffset + decoration.Triangles[triangleIndex + 1]);
                triangles.Add(vertexOffset + decoration.Triangles[triangleIndex + 2]);
            }
        }

        Mesh mesh = chunk.DecorationMesh;
        mesh.Clear();
        if (vertices.Count == 0)
        {
            chunk.DecorationRenderer.enabled = false;
            return;
        }

        mesh.indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        mesh.SetVertices(vertices);
        mesh.SetColors(colors);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        chunk.DecorationRenderer.enabled = true;
    }

    private void SpawnReleasedDecorationsAtCell(int cellIndex, Vector3 localPosition, Vector3 sawCenter,
        float pressSpeed, float outwardForce, float tangentialForce, float spinDirection,
        float bladeRadius, float sideDamping, float maxVelocity)
    {
        if (_decorationsByAnchorCell == null || (uint)cellIndex >= (uint)_decorationsByAnchorCell.Length)
            return;

        List<int> decorationIndices = _decorationsByAnchorCell[cellIndex];
        if (decorationIndices == null)
            return;

        for (int i = 0; i < decorationIndices.Count; i++)
        {
            DecorationRuntime decoration = _levelDecorations[decorationIndices[i]];
            QueueReleasedBlockSpawn(localPosition, decoration.ReleasedColor, decoration.ReleasedTypeIndex,
                sawCenter, pressSpeed, outwardForce, tangentialForce, spinDirection, bladeRadius,
                sideDamping, maxVelocity);
        }
    }

    private void MarkDecorationChunksDirty(int cellIndex)
    {
        if (_decorationChunksByCell == null || (uint)cellIndex >= (uint)_decorationChunksByCell.Length)
            return;

        List<int> chunkIndices = _decorationChunksByCell[cellIndex];
        if (chunkIndices == null)
            return;

        for (int i = 0; i < chunkIndices.Count; i++)
            MarkChunkDirty(chunkIndices[i]);
    }

    private static Color32 MultiplyColor(Color32 tint, Color32 source)
    {
        return new Color32(
            (byte)(tint.r * source.r / 255),
            (byte)(tint.g * source.g / 255),
            (byte)(tint.b * source.b / 255),
            (byte)(tint.a * source.a / 255));
    }

    private void DisposeDecorations()
    {
        for (int i = 0; i < _chunks.Count; i++)
        {
            ChunkRuntime chunk = _chunks[i];
            if (chunk.DecorationObject != null)
            {
                chunk.DecorationObject.SetActive(false);
                chunk.DecorationObject.transform.SetParent(null);
                DestroyUnityObject(chunk.DecorationObject);
            }
            if (chunk.DecorationMesh != null)
                DestroyUnityObject(chunk.DecorationMesh);

            chunk.DecorationObject = null;
            chunk.DecorationMesh = null;
            chunk.DecorationRenderer = null;
            chunk.DecorationIndices = null;
            chunk.DecorationVertices = null;
            chunk.DecorationColors = null;
            chunk.DecorationUvs = null;
            chunk.DecorationTriangles = null;
            _chunks[i] = chunk;
        }
        _levelDecorations.Clear();
        _decorationChunksByCell = null;
        _decorationsByAnchorCell = null;
    }
}
