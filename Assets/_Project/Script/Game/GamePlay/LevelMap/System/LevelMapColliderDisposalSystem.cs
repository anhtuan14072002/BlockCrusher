using System.Collections.Generic;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;

[UpdateInGroup(typeof(AfterPhysicsSystemGroup), OrderLast = true)]
[WorldSystemFilter(WorldSystemFilterFlags.Default)]
public partial class LevelMapColliderDisposalSystem : SystemBase
{
    private static readonly List<BlobAssetReference<Collider>> RetiredColliders = new(16);

    internal static void Retire(BlobAssetReference<Collider> collider)
    {
        if (collider.IsCreated)
            RetiredColliders.Add(collider);
    }

    protected override void OnUpdate()
    {
        DisposeRetiredColliders();
    }

    protected override void OnDestroy()
    {
        DisposeRetiredColliders();
    }

    private static void DisposeRetiredColliders()
    {
        for (int i = 0; i < RetiredColliders.Count; i++)
        {
            if (RetiredColliders[i].IsCreated)
                RetiredColliders[i].Dispose();
        }
        RetiredColliders.Clear();
    }
}
