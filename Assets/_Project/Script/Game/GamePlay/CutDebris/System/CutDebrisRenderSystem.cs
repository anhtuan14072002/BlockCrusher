using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

namespace Crusher
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(LevelMapSystem))]
    public partial class CutDebrisRenderSystem : SystemBase
    {
        private const int MaxInstancesPerBatch = 1023;
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private sealed class RenderBatch
        {
            public readonly Matrix4x4[] Matrices = new Matrix4x4[MaxInstancesPerBatch];
            public readonly Vector4[] Colors = new Vector4[MaxInstancesPerBatch];
            public int Count;
        }

        private readonly List<RenderBatch> _batches = new(16);
        private MaterialPropertyBlock _propertyBlock;
        private CutDebrisAuthoring _source;
        private Material _material;

        protected override void OnCreate()
        {
            _propertyBlock = new MaterialPropertyBlock();
            RequireForUpdate<CutDebrisComponent>();
        }

        protected override void OnUpdate()
        {
            CutDebrisAuthoring source = CutDebrisAuthoring.Active;
            if (source == null || source.Material == null || source.VariantCount == 0)
                return;

            if (_source != source)
            {
                DisposeMaterial();
                _source = source;
                _material = new Material(source.Material) { enableInstancing = true };
            }

            EnsureBatches(source.VariantCount);
            for (int i = 0; i < source.VariantCount; i++)
                _batches[i].Count = 0;

            foreach (var (transformReference, debrisReference) in
                     SystemAPI.Query<RefRO<LocalTransform>, RefRO<CutDebrisComponent>>())
            {
                LocalTransform transform = transformReference.ValueRO;
                CutDebrisComponent debris = debrisReference.ValueRO;
                int variantIndex = debris.VariantIndex;
                if ((uint)variantIndex >= (uint)source.VariantCount)
                    continue;

                RenderBatch batch = _batches[variantIndex];
                quaternion rotation = transform.Rotation;
                batch.Matrices[batch.Count] = Matrix4x4.TRS(
                    new Vector3(transform.Position.x, transform.Position.y, transform.Position.z),
                    new Quaternion(rotation.value.x, rotation.value.y, rotation.value.z, rotation.value.w),
                    new Vector3(debris.RenderScale.x, debris.RenderScale.y, debris.RenderScale.z) * transform.Scale);
                batch.Colors[batch.Count] = new Vector4(
                    debris.Color.x, debris.Color.y, debris.Color.z, debris.Color.w);
                batch.Count++;
                if (batch.Count == MaxInstancesPerBatch)
                {
                    DrawBatch(source, variantIndex, batch);
                    batch.Count = 0;
                }
            }

            for (int i = 0; i < source.VariantCount; i++)
            {
                if (_batches[i].Count > 0)
                    DrawBatch(source, i, _batches[i]);
            }
        }

        protected override void OnDestroy()
        {
            DisposeMaterial();
        }

        private void EnsureBatches(int count)
        {
            while (_batches.Count < count)
                _batches.Add(new RenderBatch());
        }

        private void DrawBatch(CutDebrisAuthoring source, int variantIndex, RenderBatch batch)
        {
            CutDebrisAuthoring.VariantData variant = source.GetVariant(variantIndex);
            _propertyBlock.Clear();
            _propertyBlock.SetVectorArray(ColorId, batch.Colors);
            Graphics.DrawMeshInstanced(variant.Mesh, 0, _material, batch.Matrices, batch.Count,
                _propertyBlock, ShadowCastingMode.Off, false, source.gameObject.layer);
        }

        private void DisposeMaterial()
        {
            if (_material != null)
                Object.Destroy(_material);
            _material = null;
            _source = null;
            _batches.Clear();
        }
    }
}
