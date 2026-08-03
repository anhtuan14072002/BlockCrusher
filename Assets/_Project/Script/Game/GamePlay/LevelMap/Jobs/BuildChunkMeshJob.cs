using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

[BurstCompile]
internal struct BuildChunkMeshJob : IJob
{
    [ReadOnly] public NativeArray<Color32> CellColors;
    [ReadOnly] public NativeArray<byte> CellSolid;
    [ReadOnly] public NativeArray<ushort> CellReleasedTypes;
    [ReadOnly] public NativeArray<Vector3> AuthoredMeshVertices;
    [ReadOnly] public NativeArray<Vector2> AuthoredMeshUvs;
    [ReadOnly] public NativeArray<int> AuthoredMeshIndices;
    [ReadOnly] public NativeArray<LevelMapSpawner.AuthoredMeshRange> AuthoredMeshRanges;

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
        new LevelMapSpawner.LevelMapMeshBuilder
        {
            CellColors = CellColors,
            CellSolid = CellSolid,
            CellReleasedTypes = CellReleasedTypes,
            AuthoredMeshVertices = AuthoredMeshVertices,
            AuthoredMeshUvs = AuthoredMeshUvs,
            AuthoredMeshIndices = AuthoredMeshIndices,
            AuthoredMeshRanges = AuthoredMeshRanges,
            Visited = Visited,
            Vertices = Vertices,
            Colors = Colors,
            Uvs = Uvs,
            Indices = Indices,
            GridWidth = GridWidth,
            StartX = StartX,
            StartY = StartY,
            ChunkWidth = ChunkWidth,
            ChunkHeight = ChunkHeight,
            CellSize = CellSize,
            Offset = Offset,
            ExtrudeMergedQuads = ExtrudeMergedQuads,
            MergeAnySolid = MergeAnySolid,
            UseAuthoredMeshes = UseAuthoredMeshes,
            DetailDepth = DetailDepth
        }.Execute();
    }
}
