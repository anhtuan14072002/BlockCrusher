using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

namespace Wizard
{
    public struct MiniStoneTag : IComponentData
    {
    }

    public sealed class MiniStoneRenderData : IComponentData
    {
        public Mesh Mesh;
        public Material Material;
    }

    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class MiniStoneVisualSystem : SystemBase
    {
        private const int BatchSize = 1023;
        private readonly Matrix4x4[] _matrices = new Matrix4x4[BatchSize];

        protected override void OnCreate()
        {
            RequireForUpdate<MiniStoneRenderData>();
        }

        protected override void OnUpdate()
        {
            MiniStoneRenderData renderData = SystemAPI.ManagedAPI.GetSingleton<MiniStoneRenderData>();
            if (renderData.Mesh == null || renderData.Material == null) return;

            NativeArray<LocalToWorld> transforms = GetEntityQuery(
                    ComponentType.ReadOnly<MiniStoneTag>(),
                    ComponentType.ReadOnly<LocalToWorld>())
                .ToComponentDataArray<LocalToWorld>(Allocator.Temp);

            for (int start = 0; start < transforms.Length; start += BatchSize)
            {
                int count = math.min(BatchSize, transforms.Length - start);
                for (int i = 0; i < count; i++)
                    _matrices[i] = transforms[start + i].Value;

                Graphics.DrawMeshInstanced(renderData.Mesh, 0, renderData.Material, _matrices, count,
                    null, ShadowCastingMode.Off, false);
            }

            transforms.Dispose();
        }
    }
}
