using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using UnityEngine;
using PhysicsCollider = Unity.Physics.Collider;

internal struct ChunkRuntime
{
    // Unity Mesh/Renderer hiển thị phần cell còn nguyên của chunk.
    public Mesh Mesh;
    public MeshRenderer Renderer;
    // Entity và blob collider ECS đại diện phần solid của chunk.
    public Entity PhysicsEntity;
    public BlobAssetReference<PhysicsCollider> PhysicsCollider;
    // Buffer tạm khi greedy-mesh: đánh dấu cell đã được gộp thành quad/voxel.
    public NativeArray<byte> Visited;
    public NativeList<Vector3> Vertices;
    public NativeList<Color32> Colors;
    public NativeList<Vector2> Uvs;
    public NativeList<int> Indices;
    // Bộ buffer riêng cho mesh collider để không ảnh hưởng mesh render.
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
