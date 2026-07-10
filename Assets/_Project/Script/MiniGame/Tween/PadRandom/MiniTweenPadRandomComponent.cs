using System;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Wizard
{
    [Serializable]
    public struct MiniTweenPadRandomComponent : IComponentData
    {
        public Entity PadTween;
        public LocalTransform PadTweenInitialTransform;
        public bool isInitialized;
        public float3 initializedUniformScale;
        public float3 targetUniformScale;
        public float targetScale;
    }
}