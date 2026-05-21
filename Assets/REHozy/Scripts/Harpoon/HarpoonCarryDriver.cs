using UnityEngine;
using UnityEngine.InputSystem;

namespace REHozy.Harpoon
{
    [DisallowMultipleComponent]
    [AddComponentMenu("REHozy/Harpoon/Harpoon Carry Driver")]
    public sealed class HarpoonCarryDriver : MonoBehaviour
    {
        [SerializeField] private UnityEngine.Camera targetCamera;
        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField] private float heightOffset = 0.5f;
        [SerializeField] private float rayStartHeight = 50f;
        [SerializeField] private float maxRayDistance = 200f;
        [SerializeField] private Vector3 tipForwardAxis = Vector3.forward;

        [Header("Smooth carry — empty")]
        [SerializeField] private float positionSmoothTime = 0.1f;
        [SerializeField] private float maxTiltAngle = 14f;
        [SerializeField] private float tiltDegreesPerSpeed = 4f;
        [SerializeField] private float tiltSmoothTime = 0.14f;

        [Header("Smooth carry — with cargo")]
        [SerializeField] private float positionSmoothTimeWithCargo = 0.25f;
        [SerializeField] private float maxTiltAngleWithCargo = 28f;
        [SerializeField] private float tiltDegreesPerSpeedWithCargo = 2.5f;
        [SerializeField] private float tiltSmoothTimeWithCargo = 0.28f;

        [Header("Smooth carry — shared")]
        [SerializeField] private float minTiltSpeed = 0.15f;

        private Vector3 _smoothedPosition;
        private Vector3 _positionSmoothVelocity;
        private Vector3 _lastSmoothedPosition;
        private float _currentTilt;
        private float _tiltSmoothVelocity;
        private Vector3 _lastTiltAxis = Vector3.right;
        private bool _carryMotionInitialized;

        public float HeightOffset
        {
            get => heightOffset;
            set => heightOffset = value;
        }

        public LayerMask GroundMask
        {
            get => groundMask;
            set => groundMask = value;
        }

        public bool TryGetGroundAnchor(out Vector3 groundPoint)
        {
            groundPoint = default;
            if (!TryResolveAim(out var anchor, out var groundY))
            {
                return false;
            }

            groundPoint = new Vector3(anchor.x, groundY, anchor.z);
            return true;
        }

        public bool TryGetCarryPose(out Vector3 position, out Quaternion rotation)
        {
            position = default;
            rotation = default;

            if (!TryResolveAim(out var anchor, out var groundY))
            {
                return false;
            }

            position = new Vector3(anchor.x, groundY + heightOffset, anchor.z);
            var tipDir = transform.TransformDirection(tipForwardAxis.normalized);
            if (tipDir.sqrMagnitude < 0.0001f)
            {
                tipDir = transform.forward;
            }

            rotation = Quaternion.FromToRotation(tipDir, Vector3.down) * transform.rotation;
            return true;
        }

        public void ResetCarryMotion(Vector3 worldPosition)
        {
            _smoothedPosition = worldPosition;
            _lastSmoothedPosition = worldPosition;
            _positionSmoothVelocity = Vector3.zero;
            _currentTilt = 0f;
            _tiltSmoothVelocity = 0f;
            _carryMotionInitialized = true;
        }

        /// <summary>
        /// Smooth root motion; tilt around <paramref name="tipPivot"/> opposite to horizontal movement (stronger with cargo).
        /// </summary>
        public bool TryApplySmoothedCarry(Transform root, Transform tipPivot, bool hasCargo)
        {
            if (!TryGetCarryPose(out var targetPosition, out var baseRotation))
            {
                return false;
            }

            if (!_carryMotionInitialized)
            {
                ResetCarryMotion(root.position);
            }

            var posSmooth = hasCargo ? positionSmoothTimeWithCargo : positionSmoothTime;
            var tiltCap = hasCargo ? maxTiltAngleWithCargo : maxTiltAngle;
            var tiltPerSpeed = hasCargo ? tiltDegreesPerSpeedWithCargo : tiltDegreesPerSpeed;
            var tiltSmooth = hasCargo ? tiltSmoothTimeWithCargo : tiltSmoothTime;

            _smoothedPosition = Vector3.SmoothDamp(
                _smoothedPosition,
                targetPosition,
                ref _positionSmoothVelocity,
                Mathf.Max(posSmooth, 0.01f));

            root.SetPositionAndRotation(_smoothedPosition, baseRotation);

            if (tipPivot == null)
            {
                _currentTilt = Mathf.SmoothDamp(_currentTilt, 0f, ref _tiltSmoothVelocity, tiltSmooth);
                _lastSmoothedPosition = _smoothedPosition;
                return true;
            }

            var deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
            var moveVelocity = (_smoothedPosition - _lastSmoothedPosition) / deltaTime;
            _lastSmoothedPosition = _smoothedPosition;

            var horizontalVelocity = new Vector3(moveVelocity.x, 0f, moveVelocity.z);
            var targetTilt = 0f;

            if (horizontalVelocity.sqrMagnitude >= minTiltSpeed * minTiltSpeed)
            {
                var moveDir = horizontalVelocity.normalized;
                _lastTiltAxis = Vector3.Cross(moveDir, Vector3.up);
                if (_lastTiltAxis.sqrMagnitude < 0.0001f)
                {
                    _lastTiltAxis = Vector3.right;
                }
                else
                {
                    _lastTiltAxis.Normalize();
                }

                targetTilt = Mathf.Clamp(
                    horizontalVelocity.magnitude * tiltPerSpeed,
                    0f,
                    tiltCap);
            }

            _currentTilt = Mathf.SmoothDamp(_currentTilt, targetTilt, ref _tiltSmoothVelocity, tiltSmooth);

            if (_currentTilt > 0.01f)
            {
                root.RotateAround(tipPivot.position, _lastTiltAxis, _currentTilt);
            }

            return true;
        }

        private bool TryResolveAim(out Vector3 anchor, out float groundY)
        {
            anchor = default;
            groundY = 0f;

            var cam = ResolveCamera();
            if (cam == null)
            {
                return false;
            }

            var mouse = Mouse.current;
            if (mouse == null)
            {
                return false;
            }

            var ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            if (!Physics.Raycast(ray, out var hit, maxRayDistance, groundMask, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            anchor = hit.point;
            var downOrigin = new Vector3(anchor.x, rayStartHeight, anchor.z);
            groundY = anchor.y;
            if (Physics.Raycast(downOrigin, Vector3.down, out var downHit, rayStartHeight + 10f, groundMask,
                    QueryTriggerInteraction.Ignore))
            {
                groundY = downHit.point.y;
            }

            return true;
        }

        private UnityEngine.Camera ResolveCamera()
        {
            if (targetCamera != null)
            {
                return targetCamera;
            }

            return UnityEngine.Camera.main;
        }
    }
}
