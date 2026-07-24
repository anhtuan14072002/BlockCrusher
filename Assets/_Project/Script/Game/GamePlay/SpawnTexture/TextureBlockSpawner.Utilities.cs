using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;
using PhysicsMaterial = Unity.Physics.Material;

public sealed partial class TextureBlockSpawner
{
    private PhysicsMaterial CreatePhysicsMaterial()
    {
        PhysicsMaterial material = PhysicsMaterial.Default;
        material.Friction = _physicsFriction;
        material.Restitution = _physicsRestitution;
        return material;
    }
    private static float3 ToFloat3(Vector3 value)
    {
        return new float3(value.x, value.y, value.z);
    }
    private static float4 ToFloat4(Vector4 value)
    {
        return new float4(value.x, value.y, value.z, value.w);
    }
    private static float4 ToFloat4(Color32 color)
    {
        return new float4(color.r / 255f, color.g / 255f, color.b / 255f, color.a / 255f);
    }
    private static void DestroyUnityObject(Object target)
    {
        if (Application.isPlaying) Destroy(target);
        else DestroyImmediate(target);
    }
    private static float GetVoxelRotationY(int x, int y, float maxAngle)
    {
        if (maxAngle <= 0f)
            return 0f;
        uint hash = (uint)(x * 73856093) ^ (uint)(y * 19349663);
        float normalized = (hash & 1023u) * (1f / 1023f);
        return (normalized * 2f - 1f) * maxAngle;
    }
}
