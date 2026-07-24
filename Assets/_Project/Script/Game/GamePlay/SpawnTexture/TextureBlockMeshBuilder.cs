using Unity.Collections;
using UnityEngine;

public sealed partial class TextureBlockSpawner
{
    internal struct TextureBlockMeshBuilder
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
        public byte UseVoxelDetail;
        public byte ExtrudeMergedQuads;
        public byte MergeAnySolid;
        public float DetailVoxelScale;
        public float RandomYRotation;
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
                    if (UseVoxelDetail != 0)
                    {
                        Visited[localIndex] = 1;
                        AddVoxelBox(x, y, color);
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
        private void AddVoxelBox(int x, int y, Color32 color)
        {
            float half = CellSize * DetailVoxelScale * 0.5f;
            float halfDepth = DetailDepth * 0.5f;
            float centerX = Offset.x + (StartX + x) * CellSize;
            float centerY = Offset.y + (StartY + y) * CellSize;
            float rotationY = GetRotationY(StartX + x, StartY + y);
            Vector3 frontMin = new Vector3(centerX - half, centerY - half, -halfDepth);
            Vector3 frontMax = new Vector3(centerX + half, centerY + half, -halfDepth);
            Vector3 backMin = new Vector3(centerX - half, centerY - half, halfDepth);
            Vector3 backMax = new Vector3(centerX + half, centerY + half, halfDepth);
            AddFace(
                RotateAroundCenter(new Vector3(frontMin.x, frontMin.y, frontMin.z), centerX, rotationY),
                RotateAroundCenter(new Vector3(frontMin.x, frontMax.y, frontMin.z), centerX, rotationY),
                RotateAroundCenter(new Vector3(frontMax.x, frontMax.y, frontMin.z), centerX, rotationY),
                RotateAroundCenter(new Vector3(frontMax.x, frontMin.y, frontMin.z), centerX, rotationY),
                color);
            AddFace(
                RotateAroundCenter(new Vector3(backMax.x, backMin.y, backMax.z), centerX, rotationY),
                RotateAroundCenter(new Vector3(backMax.x, backMax.y, backMax.z), centerX, rotationY),
                RotateAroundCenter(new Vector3(backMin.x, backMax.y, backMax.z), centerX, rotationY),
                RotateAroundCenter(new Vector3(backMin.x, backMin.y, backMax.z), centerX, rotationY),
                color);
            AddFace(
                RotateAroundCenter(new Vector3(frontMin.x, frontMin.y, frontMin.z), centerX, rotationY),
                RotateAroundCenter(new Vector3(backMin.x, backMin.y, backMin.z), centerX, rotationY),
                RotateAroundCenter(new Vector3(backMin.x, backMax.y, backMax.z), centerX, rotationY),
                RotateAroundCenter(new Vector3(frontMin.x, frontMax.y, frontMin.z), centerX, rotationY),
                color);
            AddFace(
                RotateAroundCenter(new Vector3(frontMax.x, frontMin.y, frontMin.z), centerX, rotationY),
                RotateAroundCenter(new Vector3(frontMax.x, frontMax.y, frontMin.z), centerX, rotationY),
                RotateAroundCenter(new Vector3(backMax.x, backMax.y, backMax.z), centerX, rotationY),
                RotateAroundCenter(new Vector3(backMax.x, backMin.y, backMax.z), centerX, rotationY),
                color);
            AddFace(
                RotateAroundCenter(new Vector3(frontMin.x, frontMax.y, frontMin.z), centerX, rotationY),
                RotateAroundCenter(new Vector3(backMin.x, backMax.y, backMax.z), centerX, rotationY),
                RotateAroundCenter(new Vector3(backMax.x, backMax.y, backMax.z), centerX, rotationY),
                RotateAroundCenter(new Vector3(frontMax.x, frontMax.y, frontMin.z), centerX, rotationY),
                color);
            AddFace(
                RotateAroundCenter(new Vector3(frontMin.x, frontMin.y, frontMin.z), centerX, rotationY),
                RotateAroundCenter(new Vector3(frontMax.x, frontMin.y, frontMin.z), centerX, rotationY),
                RotateAroundCenter(new Vector3(backMax.x, backMin.y, backMax.z), centerX, rotationY),
                RotateAroundCenter(new Vector3(backMin.x, backMin.y, backMin.z), centerX, rotationY),
                color);
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
        private float GetRotationY(int x, int y)
        {
            if (RandomYRotation <= 0f)
                return 0f;
            uint hash = (uint)(x * 73856093) ^ (uint)(y * 19349663);
            float normalized = (hash & 1023u) * (1f / 1023f);
            return (normalized * 2f - 1f) * RandomYRotation;
        }
        private Vector3 RotateAroundCenter(Vector3 point, float centerX, float degrees)
        {
            if (degrees == 0f)
                return point;
            float radians = degrees * 0.0174532924f;
            float sin = Mathf.Sin(radians);
            float cos = Mathf.Cos(radians);
            float localX = point.x - centerX;
            float rotatedX = localX * cos + point.z * sin;
            float rotatedZ = -localX * sin + point.z * cos;
            return new Vector3(centerX + rotatedX, point.y, rotatedZ);
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
