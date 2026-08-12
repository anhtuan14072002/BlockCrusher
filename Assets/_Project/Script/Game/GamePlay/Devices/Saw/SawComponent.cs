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
        public int CutSubMeshIndex;
        public float3 Scale;
        public float3 BladeSpinAxis;
        public float BladeSpinAngularSpeed;
        public float CutSweepStep;
        public float BlockEjectSpeed;
        public byte Active;
    }
}
