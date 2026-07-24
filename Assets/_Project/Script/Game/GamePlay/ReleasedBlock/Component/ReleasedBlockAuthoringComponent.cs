using Unity.Entities;

public struct ReleasedBlockAuthoringComponent : IComponentData
{
    public Entity ReleasedBlockPrefab;
    public float Scale;
}
