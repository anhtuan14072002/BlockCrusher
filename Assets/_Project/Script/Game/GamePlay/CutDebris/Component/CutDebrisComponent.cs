using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace Crusher
{
    public struct CutDebrisComponent : IComponentData
    {
        public float4 Color;
        public float3 RenderScale;
        public float3 PhysicsStepStartPosition;
        public float LockedZ;
        public float MaxPlanarSpeed;
        public float BaseScale;
        public float SuctionDistance;
        public int OwnerId;
        public ushort VariantIndex;
    }

    public struct CutDebrisSuctionTransit : IComponentData, IEnableableComponent
    {
    }

    public struct CutDebrisPendingActivation : IComponentData, IEnableableComponent
    {
        public BlobAssetReference<Collider> ContactCollider;
        public float3 InitialVelocity;
    }
}
