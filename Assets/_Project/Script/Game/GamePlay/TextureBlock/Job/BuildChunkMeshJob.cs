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
    
    public byte UseVoxelDetail;
    public byte ExtrudeMergedQuads;
    public byte MergeAnySolid;
    public float DetailVoxelScale;
    public float RandomYRotation;
    public float DetailDepth;

    public void Execute()
    {
        new TextureBlockSpawner.TextureBlockMeshBuilder
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
            UseVoxelDetail = UseVoxelDetail,
            ExtrudeMergedQuads = ExtrudeMergedQuads,
            MergeAnySolid = MergeAnySolid,
            DetailVoxelScale = DetailVoxelScale,
            RandomYRotation = RandomYRotation,
            DetailDepth = DetailDepth
        }.Execute();
    }
}
