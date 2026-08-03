using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;
using PhysicsMaterial = Unity.Physics.Material;

public sealed partial class LevelMapSpawner
{
    private float GetWorldCellSize()
    {
        if (_runtimeParent == null || _cellSize <= 0f)
            return _cellSize;

        float worldX = _runtimeParent.TransformVector(Vector3.right * _cellSize).magnitude;
        float worldY = _runtimeParent.TransformVector(Vector3.up * _cellSize).magnitude;
        return Mathf.Min(worldX, worldY);
    }

    private float GetMaxLocalUnitsPerWorldUnit()
    {
        if (_runtimeParent == null)
            return 1f;

        float localPerWorldX = _runtimeParent.InverseTransformVector(Vector3.right).magnitude;
        float localPerWorldY = _runtimeParent.InverseTransformVector(Vector3.up).magnitude;
        return Mathf.Max(localPerWorldX, localPerWorldY);
    }

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

    private static float4x4 ToFloat4x4(Matrix4x4 value)
    {
        return new float4x4(
            ToFloat4(value.GetColumn(0)),
            ToFloat4(value.GetColumn(1)),
            ToFloat4(value.GetColumn(2)),
            ToFloat4(value.GetColumn(3)));
    }

    private static void DestroyUnityObject(Object target)
    {
        if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);
    }
}
