using System;
using Unity.Entities;
using UnityEngine;

namespace Wizard
{
    [Serializable]
    public class MiniMultiAuthoring : MonoBehaviour
    {
        public int multiNumber;
        public int padId;
        public int padIdMove;
        public float radius;
        class Baker : Baker<MiniMultiAuthoring>
        {
            public override void Bake(MiniMultiAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new MiniMultiComponent
                {
                    MultiNumber = authoring.multiNumber,
                    PadId = authoring.padId,
                    Radius = authoring.radius,
                    PadIdMove = authoring.padIdMove
                });
            }
        }
    }
}