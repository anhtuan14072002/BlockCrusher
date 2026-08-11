using UnityEngine;

public sealed partial class LevelMapAuthoring
{
    internal static int ActiveSpawnerCount => ActiveSpawners.Count;
    internal static LevelMapAuthoring GetActiveSpawner(int index) => ActiveSpawners[index];
    internal static void RemoveActiveSpawnerAt(int index) => ActiveSpawners.RemoveAt(index);
    public static bool ReleaseInBoxForActiveSpawners(Matrix4x4 cutLocalToWorld, Bounds cutLocalBounds,
        Vector3[] cutVertices, int[] cutTriangles, float maxVelocity)
    {
        bool releasedAny = false;
        for (int i = ActiveSpawners.Count - 1; i >= 0; i--)
        {
            LevelMapAuthoring spawner = ActiveSpawners[i];
            if (spawner == null)
            {
                ActiveSpawners.RemoveAt(i);
                continue;
            }
            releasedAny |= spawner.ReleaseInBox(
                cutLocalToWorld, cutLocalBounds, cutVertices, cutTriangles, maxVelocity);
        }
        return releasedAny;
    }
}
