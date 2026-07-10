using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Wizard
{
    public class MiniTweenPadRandomAuthoring : MonoBehaviour
    {
        [SerializeField] public GameObject padRandom;
        [SerializeField] public float3 initializedUniformScale;
        [SerializeField] public float3 targetUniformScale;
        [SerializeField] public float targetPositionScale;
        class baker : Baker<MiniTweenPadRandomAuthoring>
        {
            public override void Bake(MiniTweenPadRandomAuthoring authoring)
            {
                AddComponent(GetEntity(TransformUsageFlags.Dynamic), new MiniTweenPadRandomComponent
                {
                    PadTween = GetEntity(authoring.padRandom, TransformUsageFlags.Dynamic),
                    initializedUniformScale = authoring.initializedUniformScale,
                    targetUniformScale = authoring.targetUniformScale,
                    targetScale = authoring.targetPositionScale,
                });
            }
        }
    }
}