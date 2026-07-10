using System;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine.Serialization;

namespace Wizard
{
    [Serializable]
    public struct MiniTweenPadMoveComponent : IComponentData
    {
        public Entity PadTween;
        public LocalTransform PadTweenInitialTransform;
        
        public float targetPosition;
        public bool isInitialized;
        public bool isStop;
    }
}