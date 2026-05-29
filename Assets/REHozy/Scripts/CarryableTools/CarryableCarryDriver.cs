using System;
using REHozy.Decoration;
using UnityEngine;

namespace REHozy.CarryableTools
{
    public enum WorkPoseOrientation
    {
        /// <summary>Tool lies on the work plane; forward follows movement (e.g. watering can).</summary>
        SlideOnSurface = 0,

        /// <summary>Tip stays aimed into the surface; only yaw follows movement (e.g. shovel).</summary>
        TipDownYaw = 1,
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("REHozy/Carryable Tools/Carryable Carry Driver")]
    public sealed class CarryableCarryDriver : MonoBehaviour
    {
        [SerializeField] private UnityEngine.Camera targetCamera;
        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField] private float heightOffset = 0.5f;
        [SerializeField] private float rayStartHeight = 50f;
        [SerializeField] private float maxRayDistance = 200f;
        [SerializeField] private Vector3 tipForwardAxis = Vector3.forward;

        [Header("Water")]
        [SerializeField] private bool clampTipAboveWater;
        [SerializeField] private float waterTipClearance = WaterCarryClamp.DefaultTipClearance;

        [Header("Work pose (optional)")]
        [SerializeField] private bool enableWorkPose;
        [SerializeField] private WorkPoseOrientation workPoseOrientation = WorkPoseOrientation.SlideOnSurface;
        [SerializeField] private float workHeightOffsetDelta = -0.12f;
        [SerializeField] private float workTurnLerpSpeed = 4f;
        [SerializeField] private float workMinTurnSpeed = 0.25f;

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
        private bool _workPoseActive;
        private Quaternion _workSmoothedRotation;
        private Vector3 _lastWorkMoveDir = Vector3.forward;

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

        public bool ClampTipAboveWater => clampTipAboveWater;

        public float WaterTipClearance => waterTipClearance;

        public bool TryGetGroundAnchor(out Vector3 groundPoint)
        {
            if (clampTipAboveWater && TryGetSurfaceAnchorUnderTip(out groundPoint, out _))
            {
                return true;
            }

            groundPoint = default;
            if (!TryResolveAim(out var anchor, out _))
            {
                return false;
            }

            groundPoint = anchor;
            return true;
        }

        public bool TryGetSurfaceAnchorUnderTip(out Vector3 anchor, out Vector3 surfaceNormal)
        {
            anchor = default;
            surfaceNormal = Vector3.up;

            var core = GetComponent<CarryableToolCore>();
            var tip = core != null ? core.Tip : null;
            if (tip == null)
            {
                return false;
            }

            var tipPosition = tip.position;
            var probeOrigin = new Vector3(tipPosition.x, rayStartHeight, tipPosition.z);
            var hasGround = TryRaycastGroundBelow(probeOrigin, out var groundHit);

            if (clampTipAboveWater
                && WaterCarryClamp.ShouldUseWaterSurfaceAt(tipPosition, groundMask, out var waterAnchor))
            {
                anchor = waterAnchor;
                surfaceNormal = Vector3.up;
                return true;
            }

            if (hasGround)
            {
                anchor = new Vector3(tipPosition.x, groundHit.point.y, tipPosition.z);
                surfaceNormal = groundHit.normal;
                return true;
            }

            return false;
        }

        public bool TryGetCarryPose(out Vector3 position, out Quaternion rotation)
        {
            position = default;
            rotation = default;

            if (!TryResolveAim(out var anchor, out var surfaceNormal))
            {
                return false;
            }

            position = anchor + surfaceNormal * heightOffset;
            var tipDir = transform.TransformDirection(tipForwardAxis.normalized);
            if (tipDir.sqrMagnitude < 0.0001f)
            {
                tipDir = transform.forward;
            }

            rotation = Quaternion.FromToRotation(tipDir, -surfaceNormal) * transform.rotation;
            return true;
        }

        public UnityEngine.Camera ResolveCameraForAim() => ResolveCamera();

        public void SetWorkPoseActive(bool active)
        {
            if (!enableWorkPose)
            {
                _workPoseActive = false;
                return;
            }

            if (active && !_workPoseActive)
            {
                _workSmoothedRotation = Quaternion.identity;
            }

            _workPoseActive = active;
        }

        public void ResetCarryMotion(Vector3 worldPosition)
        {
            _smoothedPosition = worldPosition;
            _lastSmoothedPosition = worldPosition;
            _positionSmoothVelocity = Vector3.zero;
            _currentTilt = 0f;
            _tiltSmoothVelocity = 0f;
            _carryMotionInitialized = true;
            _workSmoothedRotation = Quaternion.identity;
        }

        public bool TryApplySmoothedCarry(Transform root, Transform tipPivot, bool hasCargo)
        {
            if (!TryGetCarryPose(
                    root,
                    tipPivot,
                    out var targetPosition,
                    out var baseRotation,
                    out var surfaceNormal))
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

            if (clampTipAboveWater && tipPivot != null)
            {
                _smoothedPosition = WaterCarryClamp.ClampRootSoTipAboveWater(
                    root, tipPivot, _smoothedPosition, baseRotation, waterTipClearance, groundMask);
            }

            if (tipPivot == null)
            {
                _currentTilt = Mathf.SmoothDamp(_currentTilt, 0f, ref _tiltSmoothVelocity, tiltSmooth);
                _lastSmoothedPosition = _smoothedPosition;
                root.SetPositionAndRotation(_smoothedPosition, baseRotation);
                return true;
            }

            var deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
            var moveVelocity = (_smoothedPosition - _lastSmoothedPosition) / deltaTime;
            _lastSmoothedPosition = _smoothedPosition;

            var horizontalVelocity = new Vector3(moveVelocity.x, 0f, moveVelocity.z);

            var finalRotation = baseRotation;
            if (_workPoseActive)
            {
                finalRotation = ComputeWorkRotation(
                    baseRotation,
                    horizontalVelocity,
                    surfaceNormal,
                    deltaTime);
            }

            root.SetPositionAndRotation(_smoothedPosition, finalRotation);

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

            if (clampTipAboveWater && tipPivot != null)
            {
                root.position = WaterCarryClamp.ClampRootSoTipAboveWater(
                    root, tipPivot, root.position, root.rotation, waterTipClearance, groundMask);
            }

            return true;
        }

        private bool TryGetCarryPose(
            Transform root,
            Transform tipPivot,
            out Vector3 position,
            out Quaternion rotation,
            out Vector3 surfaceNormal)
        {
            position = default;
            rotation = default;
            surfaceNormal = Vector3.up;

            if (!TryResolveAim(out var anchor, out surfaceNormal))
            {
                return false;
            }

            var height = heightOffset;
            if (_workPoseActive)
            {
                height += workHeightOffsetDelta;
            }

            position = anchor + surfaceNormal * height;

            // Prefer the actual "tip direction" (root -> tip) so swapped meshes with different FBX axes
            // still orient correctly as long as Tip is placed at the working end.
            var tipDir = Vector3.zero;
            if (root != null && tipPivot != null)
            {
                tipDir = tipPivot.position - root.position;
            }

            if (tipDir.sqrMagnitude < 0.0001f)
            {
                tipDir = transform.TransformDirection(tipForwardAxis.normalized);
            }

            if (tipDir.sqrMagnitude < 0.0001f)
            {
                tipDir = transform.forward;
            }

            rotation = Quaternion.FromToRotation(tipDir, -surfaceNormal) * transform.rotation;

            if (clampTipAboveWater)
            {
                position = WaterCarryClamp.ClampRootSoTipAboveWater(
                    root, tipPivot, position, rotation, waterTipClearance, groundMask);
            }

            return true;
        }

        private Quaternion ComputeWorkRotation(
            Quaternion carryRotation,
            Vector3 horizontalVelocity,
            Vector3 surfaceNormal,
            float deltaTime)
        {
            var targetRotation = workPoseOrientation == WorkPoseOrientation.TipDownYaw
                ? ComputeTipDownYawWorkRotation(carryRotation, horizontalVelocity, surfaceNormal)
                : ComputeSlideOnSurfaceWorkRotation(horizontalVelocity, surfaceNormal);

            if (_workSmoothedRotation == Quaternion.identity)
            {
                _workSmoothedRotation = carryRotation;
            }

            var t = 1f - Mathf.Exp(-workTurnLerpSpeed * deltaTime);
            _workSmoothedRotation = Quaternion.Slerp(_workSmoothedRotation, targetRotation, t);
            return _workSmoothedRotation;
        }

        private Quaternion ComputeSlideOnSurfaceWorkRotation(
            Vector3 horizontalVelocity,
            Vector3 surfaceNormal)
        {
            var desiredMoveDir = _lastWorkMoveDir;
            if (horizontalVelocity.sqrMagnitude >= workMinTurnSpeed * workMinTurnSpeed)
            {
                desiredMoveDir = horizontalVelocity.normalized;
                _lastWorkMoveDir = desiredMoveDir;
            }

            var forwardOnPlane = Vector3.ProjectOnPlane(desiredMoveDir, surfaceNormal);
            if (forwardOnPlane.sqrMagnitude < 0.0001f)
            {
                forwardOnPlane = Vector3.ProjectOnPlane(transform.forward, surfaceNormal);
            }

            if (forwardOnPlane.sqrMagnitude < 0.0001f)
            {
                return transform.rotation;
            }

            forwardOnPlane.Normalize();
            return Quaternion.LookRotation(forwardOnPlane, surfaceNormal);
        }

        private Quaternion ComputeTipDownYawWorkRotation(
            Quaternion carryRotation,
            Vector3 horizontalVelocity,
            Vector3 surfaceNormal)
        {
            var desiredMoveDir = _lastWorkMoveDir;
            if (horizontalVelocity.sqrMagnitude >= workMinTurnSpeed * workMinTurnSpeed)
            {
                desiredMoveDir = horizontalVelocity.normalized;
                _lastWorkMoveDir = desiredMoveDir;
            }

            var forwardOnPlane = Vector3.ProjectOnPlane(desiredMoveDir, surfaceNormal);
            if (forwardOnPlane.sqrMagnitude < 0.0001f)
            {
                return carryRotation;
            }

            forwardOnPlane.Normalize();

            var headingOnPlane = Vector3.ProjectOnPlane(carryRotation * Vector3.forward, surfaceNormal);
            if (headingOnPlane.sqrMagnitude < 0.0001f)
            {
                headingOnPlane = Vector3.ProjectOnPlane(carryRotation * Vector3.right, surfaceNormal);
            }

            if (headingOnPlane.sqrMagnitude < 0.0001f)
            {
                return carryRotation;
            }

            headingOnPlane.Normalize();
            var yaw = Vector3.SignedAngle(headingOnPlane, forwardOnPlane, surfaceNormal);
            return Quaternion.AngleAxis(yaw, surfaceNormal) * carryRotation;
        }

        private bool TryResolveAim(out Vector3 anchor, out Vector3 surfaceNormal)
        {
            anchor = default;
            surfaceNormal = Vector3.up;

            var cam = ResolveCamera();
            if (cam == null)
            {
                return false;
            }

            var aimOverride = GetComponent<ICarryableAimOverride>();
            if (aimOverride != null
                && aimOverride.TryOverrideAim(cam, out anchor, out surfaceNormal))
            {
                return true;
            }

            if (!CarryableMouseRay.TryGetRay(cam, out var ray))
            {
                return false;
            }

            if (!TryRaycastAimSurface(ray, out anchor, out surfaceNormal)
                && !TrySampleWaterAlongRay(ray, out anchor, out surfaceNormal))
            {
                return false;
            }

            if (TryApplyWaterSurfaceAnchor(ref anchor, ref surfaceNormal))
            {
                return true;
            }

            var downOrigin = new Vector3(anchor.x, rayStartHeight, anchor.z);
            if (TryRaycastGroundBelow(downOrigin, out var downHit))
            {
                anchor = new Vector3(anchor.x, downHit.point.y, anchor.z);
                surfaceNormal = Vector3.up;
            }

            return true;
        }

        private bool TryApplyWaterSurfaceAnchor(ref Vector3 anchor, ref Vector3 surfaceNormal)
        {
            if (!clampTipAboveWater)
            {
                return false;
            }

            var core = GetComponent<CarryableToolCore>();
            var tip = core != null ? core.Tip : null;
            if (tip == null || !WaterCarryClamp.IsOverWaterAt(tip.position))
            {
                return false;
            }

            if (!WaterCarryClamp.ShouldUseWaterSurfaceAt(anchor, groundMask, out var waterAnchor))
            {
                return false;
            }

            anchor = waterAnchor;
            surfaceNormal = Vector3.up;
            return true;
        }

        private bool TrySampleWaterAlongRay(Ray ray, out Vector3 anchor, out Vector3 surfaceNormal)
        {
            anchor = default;
            surfaceNormal = Vector3.up;

            if (!clampTipAboveWater)
            {
                return false;
            }

            var core = GetComponent<CarryableToolCore>();
            var tip = core != null ? core.Tip : null;
            if (tip == null || !WaterCarryClamp.IsOverWaterAt(tip.position))
            {
                return false;
            }

            const float step = 2f;
            for (var dist = 0f; dist <= maxRayDistance; dist += step)
            {
                var sample = ray.GetPoint(dist);
                if (!WaterCarryClamp.ShouldUseWaterSurfaceAt(sample, groundMask, out anchor))
                {
                    continue;
                }

                surfaceNormal = Vector3.up;
                return true;
            }

            return false;
        }

        private bool TryRaycastAimSurface(Ray ray, out Vector3 anchor, out Vector3 surfaceNormal)
        {
            anchor = default;
            surfaceNormal = Vector3.up;

            var hits = Physics.RaycastAll(ray, maxRayDistance, groundMask, QueryTriggerInteraction.Ignore);
            if (hits.Length == 0)
            {
                return false;
            }

            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            if (clampTipAboveWater)
            {
                foreach (var hit in hits)
                {
                    if (hit.collider != null && WaterCarryClamp.IsWaterLayer(hit.collider.gameObject.layer))
                    {
                        continue;
                    }

                    anchor = hit.point;
                    surfaceNormal = hit.normal;
                    return true;
                }

                foreach (var hit in hits)
                {
                    if (hit.collider == null || !WaterCarryClamp.IsWaterLayer(hit.collider.gameObject.layer))
                    {
                        continue;
                    }

                    if (WaterCarryClamp.ShouldUseWaterSurfaceAt(hit.point, groundMask, out anchor))
                    {
                        surfaceNormal = Vector3.up;
                        return true;
                    }

                    if (WaterCarryClamp.TryGetGroundSurfaceAnchor(
                            hit.point, groundMask, out anchor, out surfaceNormal))
                    {
                        return true;
                    }
                }

                return false;
            }

            anchor = hits[0].point;
            surfaceNormal = hits[0].normal;
            return true;
        }

        private bool TryRaycastGroundBelow(Vector3 origin, out RaycastHit bestHit)
        {
            bestHit = default;
            var hits = Physics.RaycastAll(
                origin,
                Vector3.down,
                rayStartHeight + 10f,
                groundMask,
                QueryTriggerInteraction.Ignore);

            if (hits.Length == 0)
            {
                return false;
            }

            var found = false;
            var bestY = float.MinValue;

            foreach (var hit in hits)
            {
                if (hit.collider == null)
                {
                    continue;
                }

                if (clampTipAboveWater && WaterCarryClamp.IsWaterLayer(hit.collider.gameObject.layer))
                {
                    continue;
                }

                if (hit.point.y > bestY)
                {
                    bestY = hit.point.y;
                    bestHit = hit;
                    found = true;
                }
            }

            return found;
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
