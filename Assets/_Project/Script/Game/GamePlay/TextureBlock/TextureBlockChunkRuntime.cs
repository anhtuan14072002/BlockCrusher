using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using UnityEngine;
using Collider = Unity.Physics.Collider;

internal struct ChunkRuntime
{
    public Mesh Mesh;
    public MeshRenderer Renderer;
    public Entity PhysicsEntity;
    public BlobAssetReference<Collider> PhysicsCollider;

    public NativeArray<byte> Visited;
    public NativeList<Vector3> Vertices;
    public NativeList<Color32> Colors;
    public NativeList<Vector2> Uvs;
    public NativeList<int> Indices;

    public NativeArray<byte> ColliderVisited;
    public NativeList<Vector3> ColliderVertices;
    public NativeList<Color32> ColliderColors;
    public NativeList<Vector2> ColliderUvs;
    public NativeList<int> ColliderIndices;

    public GameObject DecorationObject;
    public Mesh DecorationMesh;
    public MeshRenderer DecorationRenderer;
    public int[] DecorationIndices;
    public List<Vector3> DecorationVertices;
    public List<Color32> DecorationColors;
    public List<Vector2> DecorationUvs;
    public List<int> DecorationTriangles;

    public int StartX;
    public int StartY;
    public int Width;
    public int Height;
}
