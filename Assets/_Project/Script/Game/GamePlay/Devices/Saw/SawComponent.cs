using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Crusher
{
    public enum CuttingDeviceKind : byte
    {
        Saw,
        Drill
    }

    public struct SawDeviceTag : IComponentData
    {
    }

    public struct DrillDeviceTag : IComponentData
    {
    }

    public struct SawComponent : IComponentData
    {
        public UnityObjectRef<Mesh> CutMesh;
        public float3 Scale;
        public float CutSweepStep;
        public float MaxReleasedBlockVelocity;
        public byte Active;
    }
}
