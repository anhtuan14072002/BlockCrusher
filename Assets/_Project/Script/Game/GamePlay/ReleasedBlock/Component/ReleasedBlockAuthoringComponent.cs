using Unity.Entities;

public struct ReleasedBlockAuthoringComponent : IComponentData
{
    // Đánh dấu entity authoring đã được baker tạo cho prefab debris.
    public Entity ReleasedBlockPrefab;
    public float Scale;
}
