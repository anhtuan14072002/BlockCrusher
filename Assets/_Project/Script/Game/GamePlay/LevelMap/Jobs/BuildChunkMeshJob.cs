using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

[BurstCompile]
internal struct BuildChunkMeshJob : IJob
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
        new LevelMapSpawner.LevelMapMeshBuilder
        {
            CellColors = CellColors,
            CellSolid = CellSolid,
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
            DetailDepth = DetailDepth,
            CellIrregularity = CellIrregularity
        }.Execute();
    }
}
