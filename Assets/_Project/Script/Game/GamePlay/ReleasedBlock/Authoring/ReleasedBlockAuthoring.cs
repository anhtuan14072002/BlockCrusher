using Unity.Entities;
using UnityEngine;

public sealed class ReleasedBlockAuthoring : MonoBehaviour
{
    [SerializeField] private GameObject _releasedBlockPrefab;
    [SerializeField] private float _scale = 1f;

    public GameObject ReleasedBlockPrefab => _releasedBlockPrefab;
    public float Scale => _scale;

    private sealed class Baker : Baker<ReleasedBlockAuthoring>
    {
        public override void Bake(ReleasedBlockAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new ReleasedBlockAuthoringComponent
            {
                ReleasedBlockPrefab = GetEntity(authoring._releasedBlockPrefab, TransformUsageFlags.Dynamic),
                Scale = authoring._scale
            });
        }
    }
}
