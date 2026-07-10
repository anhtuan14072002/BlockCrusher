using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace Wizard
{
    public class MiniTweenPadMoveAuthoring : MonoBehaviour
    {
        [SerializeField] public GameObject padTween;
        [SerializeField] public float targetPosition;

        class Baker : Baker<MiniTweenPadMoveAuthoring>
        {
            public override void Bake(MiniTweenPadMoveAuthoring authoring)
            {
                AddComponent(GetEntity(TransformUsageFlags.None), new MiniTweenPadMoveComponent
                {
                    PadTween = GetEntity(authoring.padTween, TransformUsageFlags.Dynamic),
                    targetPosition = authoring.targetPosition,
                });
            }
        }
    }
}