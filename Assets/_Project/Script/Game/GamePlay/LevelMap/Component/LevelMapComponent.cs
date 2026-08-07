using Unity.Entities;

public struct LevelMapComponent : IComponentData
{
    public int OwnerId;
    public int CurrentLevel;
    public byte Spawned;
}
