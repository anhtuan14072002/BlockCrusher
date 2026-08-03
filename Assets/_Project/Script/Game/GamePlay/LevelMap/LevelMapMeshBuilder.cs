using Unity.Collections;
using UnityEngine;

public sealed partial class LevelMapSpawner
{
    internal struct LevelMapMeshBuilder
    {
        [ReadOnly] public NativeArray<Color32> CellColors;
        [ReadOnly] public NativeArray<byte> CellSolid;
        [ReadOnly] public NativeArray<ushort> CellReleasedTypes;
        [ReadOnly] public NativeArray<Vector3> AuthoredMeshVertices;
        [ReadOnly] public NativeArray<Vector2> AuthoredMeshUvs;
        [ReadOnly] public NativeArray<int> AuthoredMeshIndices;
        [ReadOnly] public NativeArray<AuthoredMeshRange> AuthoredMeshRanges;
        public NativeArray<byte> Visited;
        public NativeList<Vector3> Vertices;
        public NativeList<Color32> Colors;
        public NativeList<Vector2> Uvs;
        public NativeList<int> Indices;
        public int GridWidth;
        public int StartX;
        public int StartY;
        public int ChunkWidth;
        public int ChunkHeight;
        public float CellSize;
        public Vector3 Offset;
        public byte ExtrudeMergedQuads;
        public byte MergeAnySolid;
        public byte UseAuthoredMeshes;
        public float DetailDepth;
        public void Execute()
        {
            for (int i = 0; i < Visited.Length; i++)
                Visited[i] = 0;
            for (int y = 0; y < ChunkHeight; y++)
            {
                for (int x = 0; x < ChunkWidth; x++)
                {
                    int localIndex = y * ChunkWidth + x;
                    if (Visited[localIndex] != 0)
                        continue;
                    int cellIndex = (StartY + y) * GridWidth + StartX + x;
                    if (CellSolid[cellIndex] == 0)
                        continue;
                    Color32 color = CellColors[cellIndex];
                    if (UseAuthoredMeshes != 0)
                    {
                        Visited[localIndex] = 1;
                        AddAuthoredBlock(x, y, color, CellReleasedTypes[cellIndex]);
                        continue;
                    }
                    int rectWidth = 1;
                    while (x + rectWidth < ChunkWidth && CanMerge(x + rectWidth, y, color))
                        rectWidth++;
                    int rectHeight = 1;
                    bool canGrow = true;
                    while (y + rectHeight < ChunkHeight && canGrow)
                    {
                        for (int scanX = 0; scanX < rectWidth; scanX++)
                        {
                            if (!CanMerge(x + scanX, y + rectHeight, color))
                            {
                                canGrow = false;
                                break;
                            }
                        }
                        if (canGrow)
                            rectHeight++;
                    }
                    for (int fillY = 0; fillY < rectHeight; fillY++)
                    {
                        for (int fillX = 0; fillX < rectWidth; fillX++)
                            Visited[(y + fillY) * ChunkWidth + x + fillX] = 1;
                    }
                    if (ExtrudeMergedQuads != 0)
                        AddMergedBox(x, y, rectWidth, rectHeight, color);
                    else
                        AddQuad(x, y, rectWidth, rectHeight, color);
                }
            }
        }
        private bool CanMerge(int x, int y, Color32 color)
        {
            int localIndex = y * ChunkWidth + x;
            int cellIndex = (StartY + y) * GridWidth + StartX + x;
            if (Visited[localIndex] != 0 || CellSolid[cellIndex] == 0)
                return false;
            Color32 other = CellColors[cellIndex];
            if (MergeAnySolid != 0)
                return true;
            return other.r == color.r && other.g == color.g && other.b == color.b && other.a == color.a;
        }
        private void AddAuthoredBlock(int x, int y, Color32 color, ushort typeIndex)
        {
            AuthoredMeshRange range = AuthoredMeshRanges[typeIndex];
            Vector3 center = new Vector3(
                Offset.x + (StartX + x) * CellSize,
                Offset.y + (StartY + y) * CellSize,
                0f);
            int vertexIndex = Vertices.Length;
            for (int i = 0; i < range.VertexCount; i++)
            {
                int sourceIndex = range.VertexStart + i;
                Vertices.Add(center + AuthoredMeshVertices[sourceIndex] * CellSize);
                Colors.Add(color);
                Uvs.Add(AuthoredMeshUvs[sourceIndex]);
            }
            for (int i = 0; i < range.IndexCount; i++)
                Indices.Add(vertexIndex + AuthoredMeshIndices[range.IndexStart + i]);
        }
        private void AddQuad(int x, int y, int width, int height, Color32 color)
        {
            float minX = Offset.x + (StartX + x) * CellSize - CellSize * 0.5f;
            float minY = Offset.y + (StartY + y) * CellSize - CellSize * 0.5f;
            float maxX = minX + width * CellSize;
            float maxY = minY + height * CellSize;
            int vertexIndex = Vertices.Length;
            Vertices.Add(new Vector3(minX, minY, 0f));
            Vertices.Add(new Vector3(minX, maxY, 0f));
            Vertices.Add(new Vector3(maxX, maxY, 0f));
            Vertices.Add(new Vector3(maxX, minY, 0f));
            AddVertexData(color);
            AddQuadIndices(vertexIndex);
        }
        private void AddMergedBox(int x, int y, int width, int height, Color32 color)
        {
            float minX = Offset.x + (StartX + x) * CellSize - CellSize * 0.5f;
            float minY = Offset.y + (StartY + y) * CellSize - CellSize * 0.5f;
            float maxX = minX + width * CellSize;
            float maxY = minY + height * CellSize;
            float halfDepth = DetailDepth * 0.5f;
            Vector3 frontMin = new Vector3(minX, minY, -halfDepth);
            Vector3 frontMax = new Vector3(maxX, maxY, -halfDepth);
            Vector3 backMin = new Vector3(minX, minY, halfDepth);
            Vector3 backMax = new Vector3(maxX, maxY, halfDepth);
            AddFace(new Vector3(frontMin.x, frontMin.y, frontMin.z), new Vector3(frontMin.x, frontMax.y, frontMin.z),
                new Vector3(frontMax.x, frontMax.y, frontMin.z), new Vector3(frontMax.x, frontMin.y, frontMin.z),
                color);
            AddFace(new Vector3(backMax.x, backMin.y, backMax.z), new Vector3(backMax.x, backMax.y, backMax.z),
                new Vector3(backMin.x, backMax.y, backMax.z), new Vector3(backMin.x, backMin.y, backMax.z), color);
            AddFace(new Vector3(frontMin.x, frontMin.y, frontMin.z), new Vector3(backMin.x, backMin.y, backMin.z),
                new Vector3(backMin.x, backMax.y, backMax.z), new Vector3(frontMin.x, frontMax.y, frontMin.z),
                color);
            AddFace(new Vector3(frontMax.x, frontMin.y, frontMin.z), new Vector3(frontMax.x, frontMax.y, frontMin.z),
                new Vector3(backMax.x, backMax.y, backMax.z), new Vector3(backMax.x, backMin.y, backMax.z), color);
            AddFace(new Vector3(frontMin.x, frontMax.y, frontMin.z), new Vector3(backMin.x, backMax.y, backMax.z),
                new Vector3(backMax.x, backMax.y, backMax.z), new Vector3(frontMax.x, frontMax.y, frontMin.z),
                color);
            AddFace(new Vector3(frontMin.x, frontMin.y, frontMin.z), new Vector3(frontMax.x, frontMin.y, frontMin.z),
                new Vector3(backMax.x, backMin.y, backMax.z), new Vector3(backMin.x, backMin.y, backMin.z), color);
        }
        private void AddFace(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color32 color)
        {
            int vertexIndex = Vertices.Length;
            Vertices.Add(a);
            Vertices.Add(b);
            Vertices.Add(c);
            Vertices.Add(d);
            AddVertexData(color);
            AddQuadIndices(vertexIndex);
        }
        private void AddVertexData(Color32 color)
        {
            Colors.Add(color);
            Colors.Add(color);
            Colors.Add(color);
            Colors.Add(color);
            Uvs.Add(new Vector2(0f, 0f));
            Uvs.Add(new Vector2(0f, 1f));
            Uvs.Add(new Vector2(1f, 1f));
            Uvs.Add(new Vector2(1f, 0f));
        }
        private void AddQuadIndices(int vertexIndex)
        {
            Indices.Add(vertexIndex);
            Indices.Add(vertexIndex + 1);
            Indices.Add(vertexIndex + 2);
            Indices.Add(vertexIndex);
            Indices.Add(vertexIndex + 2);
            Indices.Add(vertexIndex + 3);
        }
    }
}
