using Unity.Entities;

[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial struct LevelMapSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        for (int i = LevelMapAuthoring.ActiveSpawnerCount - 1; i >= 0; i--)
        {
            LevelMapAuthoring map = LevelMapAuthoring.GetActiveSpawner(i);
            if (map == null)
            {
                LevelMapAuthoring.RemoveActiveSpawnerAt(i);
                continue;
            }

            map.UpdateMap();
        }
    }
}
