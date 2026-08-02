using Unity.Collections;
using UnityEngine;

public sealed partial class LevelMapSpawner
{
    internal struct LevelMapMeshBuilder
    {
        [ReadOnly] public NativeArray<Color32> CellColors;
        [ReadOnly] public NativeArray<byte> CellSolid;
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
        public float DetailDepth;
        public float CellIrregularity;
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
                    if (CellIrregularity > 0f && ExtrudeMergedQuads == 0)
                    {
                        Visited[localIndex] = 1;
                        AddIrregularQuad(x, y, color);
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
        private void AddIrregularQuad(int x, int y, Color32 color)
        {
            int cellX = StartX + x;
            int cellY = StartY + y;
            float centerX = Offset.x + cellX * CellSize;
            float centerY = Offset.y + cellY * CellSize;
            float halfSize = CellSize * 0.5f;
            Vector2 bottomLeftOffset = GetGridCornerOffset(cellX, cellY, CellIrregularity) * CellSize;
            Vector2 topLeftOffset = GetGridCornerOffset(cellX, cellY + 1, CellIrregularity) * CellSize;
            Vector2 topRightOffset = GetGridCornerOffset(cellX + 1, cellY + 1, CellIrregularity) * CellSize;
            Vector2 bottomRightOffset = GetGridCornerOffset(cellX + 1, cellY, CellIrregularity) * CellSize;
            int vertexIndex = Vertices.Length;
            Vertices.Add(new Vector3(centerX - halfSize + bottomLeftOffset.x,
                centerY - halfSize + bottomLeftOffset.y, 0f));
            Vertices.Add(new Vector3(centerX - halfSize + topLeftOffset.x,
                centerY + halfSize + topLeftOffset.y, 0f));
            Vertices.Add(new Vector3(centerX + halfSize + topRightOffset.x,
                centerY + halfSize + topRightOffset.y, 0f));
            Vertices.Add(new Vector3(centerX + halfSize + bottomRightOffset.x,
                centerY - halfSize + bottomRightOffset.y, 0f));
            AddVertexData(color);
            AddQuadIndices(vertexIndex);
        }
        internal static Vector2 GetGridCornerOffset(int gridX, int gridY, float amount)
        {
            switch ((gridX & 1) | ((gridY & 1) << 1))
            {
                case 0: return new Vector2(-amount, -amount * 0.45f);
                case 1: return new Vector2(amount * 0.65f, amount * 0.9f);
                case 2: return new Vector2(-amount * 0.5f, amount * 0.75f);
                default: return new Vector2(amount * 0.85f, -amount * 0.65f);
            }
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
