using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Crusher
{
    public sealed class DeviceController : MonoBehaviour
    {
        private const float StonePushMaxStep = 0.025f;

        private enum ToolMode : byte
        {
            Saw,
            Suction,
            Drill
        }

        [Header("Input")]
        [SerializeField] private Joystick _joystick;
        [SerializeField] private Camera _movementCamera;

        [Header("Movement")]
        [SerializeField] private float _sawMoveSpeed = 3.5f;
        [SerializeField] private Vector2 _targetXBounds;
        [SerializeField] private Vector2 _targetYBounds;
        [SerializeField, Min(0f)] private float _stoneReturnDelay = 0.15f;

        [Header("Cable")]
        [SerializeField, Min(0.1f)] private float _cableMaxLength = 4.72f;
        [SerializeField, Min(0.01f)] private float _cableLengthUpgradeStep = 1.18f;
        [SerializeField] private CraneHoseVisual _cableVisual;

        [Header("Devices")]
        [SerializeField] private Transform _saw;
        [SerializeField] private SawAuthoring _sawCutter;
        [SerializeField] private SawAuthoring _drillCutter;
        [SerializeField] private SuctionAuthoring _suctionDevice;
        [SerializeField] private Button _switchToolButton;
        [SerializeField] private Button _increaseCableLengthButton;

        private readonly Vector3[] _suctionPath = new Vector3[32];
        private Transform _movementCameraTransform;
        private Quaternion _sawInputRotationOffset = Quaternion.identity;
        private Vector3 _sawTarget;
        private World _world;
        private EntityQuery _sawQuery;
        private EntityQuery _drillQuery;
        private EntityQuery _suctionQuery;
        private bool _queriesCreated;
        private ToolMode _toolMode = ToolMode.Suction;
        private bool _hasInput;
        private Vector2 _stonePushDirection;
        private float _stonePushRemaining;
        private float _stonePushSpeed;
        private float _stoneInputUnlockTime;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            _movementCamera ??= Camera.main;
            _movementCameraTransform = _movementCamera != null ? _movementCamera.transform : null;
            _cableVisual ??= GetComponent<CraneHoseVisual>();
            CacheCuttingDevices();
            _suctionDevice ??= GetComponentInChildren<SuctionAuthoring>(true);

            if (_sawCutter != null)
                _sawCutter.gameObject.SetActive(true);
            if (_drillCutter != null)
                _drillCutter.gameObject.SetActive(true);
            if (_suctionDevice != null)
                _suctionDevice.gameObject.SetActive(true);
            if (_joystick != null)
                _joystick.gameObject.SetActive(true);
            if (_switchToolButton != null)
                _switchToolButton.onClick.AddListener(ToggleMode);
            if (_increaseCableLengthButton != null)
                _increaseCableLengthButton.onClick.AddListener(IncreaseCableLength);

            _sawTarget = _saw != null ? _saw.position : transform.position;
            if (_saw != null)
                _sawInputRotationOffset = Quaternion.Inverse(GetToolRotation(Vector3.up)) * _saw.rotation;
            if (_cableVisual != null && _saw != null)
                _cableVisual.Bind(_saw, _cableMaxLength);
            ApplyModeVisuals();
        }

        private void OnDestroy()
        {
            if (_switchToolButton != null)
                _switchToolButton.onClick.RemoveListener(ToggleMode);
            if (_increaseCableLengthButton != null)
                _increaseCableLengthButton.onClick.RemoveListener(IncreaseCableLength);
            DisposeQueries();
        }

        private void OnDisable()
        {
            DisposeQueries();
        }

        private void Update()
        {
            Vector2 input = _joystick != null ? _joystick.Direction : Vector2.zero;
            _hasInput = input.sqrMagnitude > 0.0001f;
            SawAuthoring activeCutter = GetActiveCutter();
            ApplyStonePush(Time.deltaTime);
            if (_hasInput)
            {
                if (Time.time >= _stoneInputUnlockTime)
                    MoveTool(input);
                else if (activeCutter != null && activeCutter.CanBreakStone)
                    activeCutter.ClampObstacleTarget(_sawTarget, _sawTarget, Time.deltaTime, out _);
            }

            if (_hasInput && activeCutter != null)
            {
                activeCutter.BladeVisual.Rotate(
                    activeCutter.SpinAxis, activeCutter.SpinSpeed * Time.deltaTime, Space.Self);
            }
        }

        private void FixedUpdate()
        {
            if (!TryGetEntityManager(out EntityManager entityManager))
                return;

            SyncCuttingDevice(entityManager, _sawQuery, _sawCutter, _toolMode == ToolMode.Saw);
            SyncCuttingDevice(entityManager, _drillQuery, _drillCutter, _toolMode == ToolMode.Drill);
            SyncSuction(entityManager);
        }

        private void ToggleMode()
        {
            _toolMode = GetNextToolMode(_toolMode);
            ResetStoneResponse();
            ApplyModeVisuals();
        }

        private static ToolMode GetNextToolMode(ToolMode toolMode)
        {
            return toolMode switch
            {
                ToolMode.Saw => ToolMode.Suction,
                ToolMode.Suction => ToolMode.Drill,
                _ => ToolMode.Saw
            };
        }

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ValidateToolCycle()
        {
            Debug.Assert(GetNextToolMode(ToolMode.Saw) == ToolMode.Suction &&
                         GetNextToolMode(ToolMode.Suction) == ToolMode.Drill &&
                         GetNextToolMode(ToolMode.Drill) == ToolMode.Saw,
                "Device tool cycle must remain Saw -> Suction -> Drill -> Saw.");
        }
#endif

        public void IncreaseCableLength()
        {
            _cableMaxLength += _cableLengthUpgradeStep;
            _cableVisual.SetMaxLength(_cableMaxLength);
        }

        private void ApplyModeVisuals()
        {
            if (_sawCutter != null)
                _sawCutter.SetVisualActive(_toolMode == ToolMode.Saw);
            if (_drillCutter != null)
                _drillCutter.SetVisualActive(_toolMode == ToolMode.Drill);
            if (_suctionDevice != null)
                _suctionDevice.SetVisualActive(_toolMode == ToolMode.Suction);
            if (_cableVisual != null)
                _cableVisual.SetSuctionMode(_toolMode == ToolMode.Suction);

            TMP_Text label = _switchToolButton != null
                ? _switchToolButton.GetComponentInChildren<TMP_Text>(true)
                : null;
            if (label != null)
                label.text = _toolMode.ToString().ToUpperInvariant();
        }

        private void MoveTool(Vector2 input)
        {
            float lockedZ = _sawTarget.z;
            Vector3 direction = GetMoveDirection(input);
            Vector3 target = _sawTarget + direction * (_sawMoveSpeed * Time.deltaTime);
            target.x = Mathf.Clamp(target.x, _targetXBounds.x, _targetXBounds.y);
            target.y = Mathf.Clamp(target.y, _targetYBounds.x, _targetYBounds.y);

            if (_saw != null && direction.sqrMagnitude > 0.0001f)
                _saw.rotation = GetToolRotation(direction) * _sawInputRotationOffset;

            if (_cableVisual != null)
            {
                Vector3 root = _cableVisual.StartPosition;
                Vector3 offset = target - root;
                if (offset.sqrMagnitude > _cableMaxLength * _cableMaxLength)
                    target = root + offset.normalized * _cableMaxLength;
            }

            SawAuthoring activeCutter = GetActiveCutter();
            if (activeCutter != null)
            {
                Vector3 movement = target - _sawTarget;
                target = activeCutter.ClampObstacleTarget(
                    _sawTarget, target, Time.deltaTime, out bool hitStone);
                if (hitStone)
                    HandleStoneContact(activeCutter, movement);
            }

            target.z = lockedZ;
            _sawTarget = target;

            if (_saw == null)
                return;

            _saw.position = _sawTarget;
        }

        private void HandleStoneContact(SawAuthoring cutter, Vector3 movement)
        {
            _stoneInputUnlockTime = Time.time + _stoneReturnDelay;
            if (cutter.CanBreakStone || movement.sqrMagnitude <= 0.000001f)
                return;

            Vector2 pushDirection = -new Vector2(movement.x, movement.y).normalized;
            BeginStonePush(pushDirection, cutter.ObstacleBounceDistance);
        }

        private void BeginStonePush(Vector2 direction, float distance)
        {
            if (distance <= 0f || direction.sqrMagnitude <= 0.0001f)
                return;

            _stonePushDirection = direction.normalized;
            _stonePushRemaining = distance;
            _stonePushSpeed = distance / Mathf.Max(_stoneReturnDelay, 0.01f);
        }

        private void ApplyStonePush(float deltaTime)
        {
            if (_stonePushRemaining <= 0f || _saw == null)
                return;

            float distance = Mathf.Min(
                _stonePushRemaining, _stonePushSpeed * deltaTime, StonePushMaxStep);
            _stonePushRemaining -= distance;
            _sawTarget.x = Mathf.Clamp(
                _sawTarget.x + _stonePushDirection.x * distance, _targetXBounds.x, _targetXBounds.y);
            _sawTarget.y = Mathf.Clamp(
                _sawTarget.y + _stonePushDirection.y * distance, _targetYBounds.x, _targetYBounds.y);
            _saw.position = _sawTarget;
        }

        private void ResetStoneResponse()
        {
            _stonePushRemaining = 0f;
            _stonePushSpeed = 0f;
            _stoneInputUnlockTime = 0f;
        }

        private SawAuthoring GetActiveCutter()
        {
            return _toolMode switch
            {
                ToolMode.Saw => _sawCutter,
                ToolMode.Drill => _drillCutter,
                _ => null
            };
        }

        private void CacheCuttingDevices()
        {
            SawAuthoring[] cutters = GetComponentsInChildren<SawAuthoring>(true);
            for (int i = 0; i < cutters.Length; i++)
            {
                if (cutters[i].DeviceKind == CuttingDeviceKind.Drill)
                    _drillCutter ??= cutters[i];
                else
                    _sawCutter ??= cutters[i];
            }
        }

        private Vector3 GetMoveDirection(Vector2 input)
        {
            if (_movementCameraTransform == null)
                return new Vector3(input.x, input.y);

            Vector3 right = _movementCameraTransform.right;
            Vector3 up = _movementCameraTransform.up;
            right.z = 0f;
            up.z = 0f;
            Vector3 direction = right.normalized * input.x + up.normalized * input.y;
            return Vector3.ClampMagnitude(direction, 1f);
        }

        private bool TryGetEntityManager(out EntityManager entityManager)
        {
            entityManager = default;
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return false;

            if (!_queriesCreated || _world != world)
            {
                DisposeQueries();

                _world = world;
                entityManager = world.EntityManager;
                _sawQuery = entityManager.CreateEntityQuery(
                    ComponentType.ReadWrite<SawComponent>(), ComponentType.ReadWrite<DeviceBodyComponent>(),
                    ComponentType.ReadOnly<SawDeviceTag>());
                _drillQuery = entityManager.CreateEntityQuery(
                    ComponentType.ReadWrite<SawComponent>(), ComponentType.ReadWrite<DeviceBodyComponent>(),
                    ComponentType.ReadOnly<DrillDeviceTag>());
                _suctionQuery = entityManager.CreateEntityQuery(
                    ComponentType.ReadWrite<SuctionComponent>(), ComponentType.ReadWrite<DeviceBodyComponent>(),
                    ComponentType.ReadWrite<SuctionPathPoint>(), ComponentType.ReadOnly<SuctionDeviceTag>());
                _queriesCreated = true;
            }
            else
            {
                entityManager = world.EntityManager;
            }

            return true;
        }

        private void DisposeQueries()
        {
            if (!_queriesCreated)
                return;

            if (_world != null && _world.IsCreated)
            {
                _sawQuery.Dispose();
                _drillQuery.Dispose();
                _suctionQuery.Dispose();
            }

            _queriesCreated = false;
            _world = null;
        }

        private void SyncCuttingDevice(EntityManager entityManager, EntityQuery query,
            SawAuthoring cutter, bool selected)
        {
            if (cutter == null || query.CalculateEntityCount() != 1)
                return;

            Entity entity = query.GetSingletonEntity();
            SawComponent saw = entityManager.GetComponentData<SawComponent>(entity);
            DeviceBodyComponent body = entityManager.GetComponentData<DeviceBodyComponent>(entity);
            Transform source = cutter.transform;
            Quaternion rotation = source.rotation;
            Vector3 position = source.position;
            body.TargetPosition = new float3(position.x, position.y, position.z);
            body.TargetRotation = new quaternion(rotation.x, rotation.y, rotation.z, rotation.w);
            body.Active = selected ? (byte)1 : (byte)0;
            saw.Active = selected && _hasInput ? (byte)1 : (byte)0;
            entityManager.SetComponentData(entity, body);
            entityManager.SetComponentData(entity, saw);
            entityManager.SetComponentEnabled<Simulate>(entity, selected);
        }

        private void SyncSuction(EntityManager entityManager)
        {
            if (_cableVisual == null || _suctionQuery.CalculateEntityCount() != 1)
                return;

            Entity entity = _suctionQuery.GetSingletonEntity();
            SuctionComponent suction = entityManager.GetComponentData<SuctionComponent>(entity);
            bool selected = _toolMode == ToolMode.Suction;
            suction.Active = selected && _hasInput ? (byte)1 : (byte)0;
            entityManager.SetComponentData(entity, suction);
            DeviceBodyComponent body = entityManager.GetComponentData<DeviceBodyComponent>(entity);
            Transform source = _suctionDevice.transform;
            Quaternion rotation = source.rotation;
            Vector3 position = source.position;
            body.TargetPosition = new float3(position.x, position.y, position.z);
            body.TargetRotation = new quaternion(rotation.x, rotation.y, rotation.z, rotation.w);
            body.Active = selected ? (byte)1 : (byte)0;
            entityManager.SetComponentData(entity, body);
            entityManager.SetComponentEnabled<Simulate>(entity, selected);

            int pathCount = _cableVisual.CopyToolToRootPath(_suctionPath);
            DynamicBuffer<SuctionPathPoint> path = entityManager.GetBuffer<SuctionPathPoint>(entity);
            path.Clear();
            for (int i = 0; i < pathCount; i++)
            {
                Vector3 point = _suctionPath[i];
                path.Add(new SuctionPathPoint { Value = new float3(point.x, point.y, point.z) });
            }
        }

        private static Quaternion GetToolRotation(Vector3 direction)
        {
            return Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        }
    }
}
