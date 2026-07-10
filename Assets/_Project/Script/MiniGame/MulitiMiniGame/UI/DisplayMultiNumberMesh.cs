using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using TMPro;
using System.Collections.Generic;

namespace Wizard
{
    public class DisplayMultiNumberMesh : MonoBehaviour
    {
        public GameObject textMeshPrefab;

        private EntityManager _entityManager;
        private Dictionary<int, GameObject> _textMeshCache;

        private void Start()
        {
            _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            _textMeshCache = new Dictionary<int, GameObject>();
        }

        private void Update()
        {
            RefreshTextMeshes();
        }

        private void RefreshTextMeshes()
        {
            var query = _entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<MiniMultiComponent>(),
                ComponentType.ReadOnly<LocalTransform>());

            using var entities = query.ToEntityArray(Unity.Collections.Allocator.TempJob);
            foreach (var entity in entities)
            {
                ProcessEntity(entity);
            }
        }

        private void ProcessEntity(Entity entity)
        {
            var entityId = entity.Index;

            var miniMulti = _entityManager.GetComponentData<MiniMultiComponent>(entity);
            var position = _entityManager.GetComponentData<LocalTransform>(entity).Position;

            if (_textMeshCache.TryGetValue(entityId, out var textMeshObj))
            {
                UpdateTextMesh(textMeshObj, miniMulti.MultiNumber, position);
            }
            else
            {
                var newTextMesh = CreateTextMesh(entityId, miniMulti.MultiNumber, position);
                _textMeshCache[entityId] = newTextMesh;
            }
        }

        private GameObject CreateTextMesh(int entityId, int multiNumber, Vector3 position)
        {
            var textMeshObj = Instantiate(textMeshPrefab, position, Quaternion.identity);
            textMeshObj.name = $"MultiNumber_{entityId}";

            if (textMeshObj.TryGetComponent<TextMeshPro>(out var textMeshPro))
            {
                textMeshPro.text = $"x{multiNumber}";
            }

            return textMeshObj;
        }

        private void UpdateTextMesh(GameObject textMeshObj, int multiNumber, Vector3 position)
        {
            textMeshObj.transform.position = position;

            if (textMeshObj.TryGetComponent<TextMeshPro>(out var textMeshPro))
            {
                textMeshPro.text = $"x{multiNumber}";
            }
        }
    }
}
