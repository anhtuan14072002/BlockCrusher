using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Authoring;
using UnityEngine;
using PhysicsCollider = Unity.Physics.Collider;

namespace Crusher
{
    public class SuctionDevice : MonoBehaviour
    {
        [SerializeField] private Transform _suctionPoint;
        [SerializeField] private Vector3 _suctionBoxSize = new Vector3(4f, 2f, 1f);
        [SerializeField] private PhysicsShapeAuthoring _physicsShape;
        [SerializeField] private float _suctionForce = 15f;
        [SerializeField] private float _suctionAcceleration = 32f;
        [SerializeField] private float _maxBlockVelocity = 8f;

        [SerializeField] private float _arrivalDamping = 8f;
        [SerializeField] private float _tubeExitRadius = 0.08f;
        [SerializeField] private float _pathWaypointRadius = 0.08f;
        [SerializeField] private float _pathLookAhead = 0.25f;
        [SerializeField] private float _tubeRenderDepth = 0.35f;
        [SerializeField, Min(1f)] private float _rotationSpeedDegrees = 540f;

        private CraneController _craneController;
        private BlobAssetReference<PhysicsCollider> _bodyQueryCollider;
        private Vector3 _bodyQueryScale;

        private static readonly Vector3 SuctionMouthLocalDirection = Vector3.right;

        private void Awake()
        {
            _craneController = GetComponentInParent<CraneController>();
            _physicsShape ??= GetComponent<PhysicsShapeAuthoring>();
            _physicsShape ??= GetComponentInChildren<PhysicsShapeAuthoring>(true);
        }

        private void OnDestroy()
        {
            if (_bodyQueryCollider.IsCreated)
                _bodyQueryCollider.Dispose();
        }

        internal void ProcessSuction(bool allowCapture)
        {
            Transform refTransform = _suctionPoint != null ? _suctionPoint : transform;
            FixedList512Bytes<float3> suctionPath = default;
            suctionPath.Add(new float3(refTransform.position.x, refTransform.position.y, refTransform.position.z));
            if (_craneController != null)
                _craneController.AppendSuctionTubePath(ref suctionPath);

            LevelMapSpawner.ApplySuctionForActiveSpawners(
                refTransform.position, refTransform.rotation, _suctionBoxSize,
                _suctionForce, _suctionAcceleration, _maxBlockVelocity, _arrivalDamping, _tubeExitRadius,
                _pathWaypointRadius, _pathLookAhead, _tubeRenderDepth, suctionPath, Time.fixedDeltaTime,
                allowCapture);
        }

        public Vector3 ClampSawTarget(Vector3 sawPosition, Vector3 targetSawPosition)
        {
            targetSawPosition.z = sawPosition.z;
            if (!EnsureBodyQueryCollider())
                return targetSawPosition;

            Transform shapeTransform = _physicsShape.transform;
            Vector3 shapePosition = shapeTransform.position;
            Vector3 shapeTarget = shapePosition + (targetSawPosition - sawPosition);
            Vector3 clampedShapeTarget = LevelMapSpawner.ClampToolTarget(
                shapePosition, shapeTarget, _bodyQueryCollider, shapeTransform.rotation,
                GetScaledBodyCenter(), GetScaledBodyHalfSize(), GetBodyOrientation());
            return sawPosition + (clampedShapeTarget - shapePosition);
        }

        internal Quaternion ClampToolRotation(Transform toolRoot, Quaternion targetToolRotation)
        {
            if (toolRoot == null)
                return targetToolRotation;

            return Quaternion.RotateTowards(
                toolRoot.rotation, targetToolRotation,
                _rotationSpeedDegrees * Time.deltaTime);
        }

        internal Quaternion GetMovementRotation(Transform toolRoot, Vector3 movementDirection)
        {
            if (toolRoot == null || movementDirection.sqrMagnitude <= 0.00000001f)
                return toolRoot != null ? toolRoot.rotation : Quaternion.identity;

            movementDirection.z = 0f;
            movementDirection.Normalize();
            Vector3 currentMouthDirection = transform.rotation * SuctionMouthLocalDirection;
            currentMouthDirection.z = 0f;
            if (currentMouthDirection.sqrMagnitude <= 0.00000001f)
                return toolRoot.rotation;

            currentMouthDirection.Normalize();
            float angle = Vector3.SignedAngle(
                currentMouthDirection, movementDirection, Vector3.forward);
            return Quaternion.AngleAxis(angle, Vector3.forward) * toolRoot.rotation;
        }

        private Vector3 GetScaledBodyCenter()
        {
            BoxGeometry box = _physicsShape.GetBoxProperties();
            Vector3 scale = _physicsShape.transform.lossyScale;
            return new Vector3(box.Center.x * scale.x, box.Center.y * scale.y, box.Center.z * scale.z);
        }

        private Vector2 GetScaledBodyHalfSize()
        {
            BoxGeometry box = _physicsShape.GetBoxProperties();
            Vector3 scale = _physicsShape.transform.lossyScale;
            float halfWidth = Mathf.Abs(box.Size.x * scale.x) * 0.5f;
            float halfHeight = Mathf.Abs(box.Size.y * scale.y) * 0.5f;
            return new Vector2(halfWidth, halfHeight);
        }

        private Quaternion GetBodyOrientation()
        {
            quaternion orientation = _physicsShape.GetBoxProperties().Orientation;
            return new Quaternion(orientation.value.x, orientation.value.y, orientation.value.z,
                orientation.value.w);
        }

        private bool EnsureBodyQueryCollider()
        {
            if (_physicsShape == null || _physicsShape.ShapeType != ShapeType.Box)
                return false;

            Vector3 scale = _physicsShape.transform.lossyScale;
            if (_bodyQueryCollider.IsCreated && scale == _bodyQueryScale)
                return true;

            if (_bodyQueryCollider.IsCreated)
                _bodyQueryCollider.Dispose();

            BoxGeometry geometry = _physicsShape.GetBoxProperties();
            geometry.Center = new float3(
                geometry.Center.x * scale.x,
                geometry.Center.y * scale.y,
                geometry.Center.z * scale.z);
            geometry.Size = new float3(
                Mathf.Abs(geometry.Size.x * scale.x),
                Mathf.Abs(geometry.Size.y * scale.y),
                Mathf.Abs(geometry.Size.z * scale.z));
            geometry.BevelRadius *= Mathf.Min(
                Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            _bodyQueryCollider = Unity.Physics.BoxCollider.Create(geometry);
            _bodyQueryScale = scale;
            return _bodyQueryCollider.IsCreated;
        }

        private void OnDrawGizmosSelected()
        {
            Transform refTransform = _suctionPoint != null ? _suctionPoint : transform;
            Vector3 boxCenter = refTransform.position + refTransform.right * (_suctionBoxSize.x / 2f);

            Gizmos.color = new Color(0, 1, 0, 0.3f);
            Gizmos.matrix = Matrix4x4.TRS(boxCenter, refTransform.rotation, Vector3.one);
            Gizmos.DrawCube(Vector3.zero, _suctionBoxSize);
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(Vector3.zero, _suctionBoxSize);
        }
    }
}
