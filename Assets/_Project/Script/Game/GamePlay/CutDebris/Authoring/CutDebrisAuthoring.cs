using System.Collections.Generic;
using Unity.Mathematics;
using Unity.Physics.Authoring;
using UnityEngine;
using PhysicsBoxGeometry = Unity.Physics.BoxGeometry;

namespace Crusher
{
    [DisallowMultipleComponent]
    public sealed class CutDebrisAuthoring : MonoBehaviour
    {
        internal struct SpawnRequest
        {
            public Vector3 Position;
            public Color32 Color;
            public Vector3 InitialVelocity;
            public float Scale;
            public int ChunkIndex;
            public ushort VariantIndex;
        }

        internal struct VariantData
        {
            public Mesh Mesh;
            public Vector3 RenderScale;
        }

        [SerializeField] private GameObject _prefab;
        [SerializeField, Min(0.1f)] private float _maxPlanarSpeed = 4f;
        [SerializeField, Range(0.5f, 0.95f)] private float _colliderPlanarSize = 0.82f;
        [SerializeField, Range(64, 5000)] private int _maxActiveBlocks = 256;
        [SerializeField, Range(1, 256)] private int _maxSpawnsPerFrame = 24;

        private readonly List<SpawnRequest> _pending = new(256);
        private readonly List<SpawnRequest> _ready = new(128);
        private readonly List<VariantData> _variants = new(16);
        private Mesh _mesh;
        private Material _material;
        private PhysicsShapeAuthoring _physicsShape;
        private PhysicsBodyAuthoring _physicsBody;
        private int _generation;

        internal static CutDebrisAuthoring Active { get; private set; }
        internal Material Material => _material;
        internal PhysicsShapeAuthoring PhysicsShape => _physicsShape;
        internal PhysicsBodyAuthoring PhysicsBody => _physicsBody;
        internal float MaxPlanarSpeed => _maxPlanarSpeed;
        internal int MaxActiveBlocks => _maxActiveBlocks;
        internal int MaxSpawnsPerFrame => _maxSpawnsPerFrame;
        internal int ReadyCount => _ready.Count;
        internal int Generation => _generation;
        internal int VariantCount => _variants.Count;

        private void Awake()
        {
            CachePrefabData();
        }

        private void OnEnable()
        {
            Active = this;
        }

        private void OnDisable()
        {
            if (Active == this)
                Active = null;
            ResetDebris();
        }

        internal void QueueSpawn(Vector3 position, Color32 color, Mesh mesh,
            float sourceWorldScale, Vector3 renderScale, Vector3 initialVelocity, int chunkIndex)
        {
            if (_physicsShape == null || _physicsBody == null || mesh == null || sourceWorldScale <= 0f ||
                _pending.Count + _ready.Count >= _maxActiveBlocks)
                return;

            SpawnRequest request = new()
            {
                Position = position,
                Color = color,
                InitialVelocity = Vector3.ClampMagnitude(initialVelocity, _maxPlanarSpeed),
                Scale = sourceWorldScale,
                ChunkIndex = chunkIndex,
                VariantIndex = GetOrCreateVariant(mesh, renderScale)
            };
            if (chunkIndex < 0)
                _ready.Add(request);
            else
                _pending.Add(request);
        }

        internal void NotifyChunkRebuilt(int chunkIndex)
        {
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                SpawnRequest request = _pending[i];
                if (request.ChunkIndex != chunkIndex)
                    continue;

                _ready.Add(request);
                int lastIndex = _pending.Count - 1;
                _pending[i] = _pending[lastIndex];
                _pending.RemoveAt(lastIndex);
            }
        }

        internal SpawnRequest PopReady()
        {
            int lastIndex = _ready.Count - 1;
            SpawnRequest request = _ready[lastIndex];
            _ready.RemoveAt(lastIndex);
            return request;
        }

        internal void ResetDebris()
        {
            _pending.Clear();
            _ready.Clear();
            _variants.Clear();
            _generation++;
        }

        internal VariantData GetVariant(int index)
        {
            return _variants[index];
        }

        internal PhysicsBoxGeometry GetBoxGeometry(int variantIndex)
        {
            VariantData variant = _variants[variantIndex];
            PhysicsBoxGeometry template = _physicsShape.GetBoxProperties();
            PhysicsBoxGeometry geometry = template;
            Bounds prefabBounds = _mesh.bounds;
            Bounds variantBounds = variant.Mesh.bounds;
            float3 prefabSize = ToFloat3(prefabBounds.size);
            float3 variantSize = ToFloat3(Vector3.Scale(variantBounds.size, Abs(variant.RenderScale)));
            float3 sizeRatio = new(
                prefabSize.x > 0.0001f ? geometry.Size.x / prefabSize.x : 1f,
                prefabSize.y > 0.0001f ? geometry.Size.y / prefabSize.y : 1f,
                prefabSize.z > 0.0001f ? geometry.Size.z / prefabSize.z : 1f);
            float3 normalizedCenterOffset = new(
                prefabSize.x > 0.0001f ? (geometry.Center.x - prefabBounds.center.x) / prefabSize.x : 0f,
                prefabSize.y > 0.0001f ? (geometry.Center.y - prefabBounds.center.y) / prefabSize.y : 0f,
                prefabSize.z > 0.0001f ? (geometry.Center.z - prefabBounds.center.z) / prefabSize.z : 0f);
            Vector3 scaledCenter = Vector3.Scale(variantBounds.center, variant.RenderScale);
            float3 center = ToFloat3(scaledCenter) + normalizedCenterOffset * variantSize;
            float3 size = variantSize * sizeRatio;
            size.xy = math.min(size.xy, new float2(_colliderPlanarSize));
            float2 centerLimit = math.max(new float2(0.5f) - size.xy * 0.5f, float2.zero);
            center.xy = math.clamp(center.xy, -centerLimit, centerLimit);
            geometry.Center = center;
            geometry.Size = size;
            float sourceMinSize = math.cmin(template.Size);
            float targetMinSize = math.cmin(geometry.Size);
            geometry.BevelRadius = sourceMinSize > 0.0001f
                ? math.min(template.BevelRadius / sourceMinSize * targetMinSize,
                    targetMinSize * 0.49f)
                : 0f;
            return geometry;
        }

        private ushort GetOrCreateVariant(Mesh mesh, Vector3 renderScale)
        {
            for (ushort i = 0; i < _variants.Count; i++)
            {
                VariantData variant = _variants[i];
                if (variant.Mesh == mesh && (variant.RenderScale - renderScale).sqrMagnitude <= 0.000001f)
                    return i;
            }

            _variants.Add(new VariantData { Mesh = mesh, RenderScale = renderScale });
            return (ushort)(_variants.Count - 1);
        }

        private static Vector3 Abs(Vector3 value)
        {
            return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        }

        private static float3 ToFloat3(Vector3 value)
        {
            return new float3(value.x, value.y, value.z);
        }

        private void CachePrefabData()
        {
            if (_prefab == null)
            {
                Debug.LogError("CutDebrisAuthoring needs a debris prefab.", this);
                return;
            }

            MeshFilter meshFilter = _prefab.GetComponent<MeshFilter>();
            Renderer meshRenderer = _prefab.GetComponent<Renderer>();
            _physicsShape = _prefab.GetComponent<PhysicsShapeAuthoring>();
            _physicsBody = _prefab.GetComponent<PhysicsBodyAuthoring>();
            if (meshFilter == null || meshFilter.sharedMesh == null || meshRenderer == null ||
                meshRenderer.sharedMaterial == null || _physicsShape == null || _physicsBody == null)
            {
                Debug.LogError(
                    "Cut debris prefab needs MeshFilter, Renderer, PhysicsShapeAuthoring and PhysicsBodyAuthoring.",
                    _prefab);
                return;
            }

            _mesh = meshFilter.sharedMesh;
            _material = meshRenderer.sharedMaterial;
        }
    }
}
