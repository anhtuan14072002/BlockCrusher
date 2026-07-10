using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.Serialization;

namespace Wizard
{
    public class MiniJumpPadAuthoring : MonoBehaviour
    {
        public float3 jumpForce;
        public int padId;
        public int jumpForceXMin;
        public int jumpForceXMax;
        class Baker : Baker<MiniJumpPadAuthoring>
        {
            public override void Bake(MiniJumpPadAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new MiniJumpPadComponent
                {
                    JumpForce = authoring.jumpForce,
                    PadId = authoring.padId,
                    JumpForceXMin = authoring.jumpForceXMin,
                    JumpForceXMax = authoring.jumpForceXMax
                }); }
        }
    }
}