using Unity.Collections;

using Unity.Entities;

using Unity.Physics;

using UnityEngine;

using PhysicsCollider = Unity.Physics.Collider;
internal struct ChunkRuntime
{
    public Mesh Mesh;
    
    public MeshRenderer Renderer;
    
    public Entity PhysicsEntity;
    
    public BlobAssetReference<PhysicsCollider> PhysicsCollider;
    
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
    
    public int StartX;
    
    public int StartY;
    
    public int Width;
    
    public int Height;
    
    public bool IsDetailed;

}
