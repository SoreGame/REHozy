using System;
using REHozy;
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
        [SerializeField] private float carryTurnLerpSpeed = 10f;
        [SerializeField] private float aimAnchorSmoothTime = 0.08f;
        [Tooltip("Keeps the tool aligned to world up; only movement-speed tilt deviates from vertical.")]
        [SerializeField] private bool lockWorldUpright;

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
        private Vector3 _lastYawDirection = Vector3.forward;
        private Vector3 _prevAimAnchorFlat;
        private bool _hasPrevAimAnchorFlat;
        private Vector3 _lastAimAnchor;
        private bool _hasLastAimAnchor;
        private Vector3 _smoothedAimAnchor;
        private bool _smoothedAimInitialized;
        private Vector3 _bindLocalTipDirection = Vector3.forward;
        private Vector3 _bindLocalTipOffset;
        private bool _bindTipCached;
        private Quaternion _smoothedCarryRotation;
        private bool _carryRotationInitialized;
        private ICarryableCarryRotationModifier _carryRotationModifier;
        private Quaternion _dbgLastWrittenRotation = Quaternion.identity;
        private Vector3 _dbgLastWrittenPosition;
        private bool _dbgHasLastWritten;

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
            if (DecorationPlacementUtility.TrySampleTopGroundAt(
                    tipPosition, groundMask, null, out var groundHit))
            {
                if (clampTipAboveWater
                    && WaterCarryClamp.ShouldUseWaterSurfaceAt(tipPosition, groundMask, out var waterAnchor))
                {
                    anchor = waterAnchor;
                    surfaceNormal = Vector3.up;
                    return true;
                }

                anchor = new Vector3(tipPosition.x, groundHit.point.y, tipPosition.z);
                surfaceNormal = groundHit.normal.sqrMagnitude > 0.0001f ? groundHit.normal : Vector3.up;
                return true;
            }

            if (clampTipAboveWater
                && WaterCarryClamp.ShouldUseWaterSurfaceAt(tipPosition, groundMask, out var waterOnlyAnchor))
            {
                anchor = waterOnlyAnchor;
                surfaceNormal = Vector3.up;
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
                _workSmoothedRotation = _carryRotationInitialized
                    ? _smoothedCarryRotation
                    : transform.rotation;
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
            _bindTipCached = false;
            _carryRotationInitialized = false;
            _smoothedAimInitialized = false;

            var forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            _lastYawDirection = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
            _hasPrevAimAnchorFlat = false;

            var core = GetComponent<CarryableToolCore>();
            if (core != null && core.Tip != null)
            {
                CacheBindTipPose(transform, core.Tip);
            }
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

            var deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
            var dbgEntryPos = root.position;
            var dbgEntryRot = root.rotation;
            var dbgIsWateringCan = GetComponent<REHozy.Watering.WateringCanToolActions>() != null;

            if (tipPivot == null)
            {
                _currentTilt = Mathf.SmoothDamp(_currentTilt, 0f, ref _tiltSmoothVelocity, tiltSmooth);
                _lastSmoothedPosition = _smoothedPosition;
                var stableRotation = ApplyModifierPitch(SmoothCarryRotation(
                    ComputeStableCarryRotation(root, surfaceNormal),
                    deltaTime));
                root.SetPositionAndRotation(_smoothedPosition, stableRotation);
                return true;
            }

            var moveVelocity = (_smoothedPosition - _lastSmoothedPosition) / deltaTime;
            _lastSmoothedPosition = _smoothedPosition;

            var horizontalVelocity = new Vector3(moveVelocity.x, 0f, moveVelocity.z);

            var smoothBase = SmoothCarryRotation(
                ComputeStableCarryRotation(root, surfaceNormal),
                deltaTime);
            var finalRotation = ApplyModifierPitch(smoothBase);
            ResolveCarryRotationModifier();
            var dbgPitchDelta = _carryRotationModifier != null && _carryRotationModifier.UsesYawPitchCarry
                ? Quaternion.Angle(smoothBase, finalRotation)
                : 0f;
            if (_workPoseActive)
            {
                finalRotation = ComputeWorkRotation(
                    finalRotation,
                    tipPivot,
                    horizontalVelocity,
                    ResolveCarryUpVector(surfaceNormal),
                    deltaTime);
            }

            root.SetPositionAndRotation(_smoothedPosition, finalRotation);
            var dbgRotBeforeTilt = root.rotation;

            var targetTilt = 0f;

            if (!_workPoseActive
                && _carryRotationModifier == null
                && horizontalVelocity.sqrMagnitude >= minTiltSpeed * minTiltSpeed)
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
                var tiltPivot = root.position + finalRotation * _bindLocalTipOffset;
                root.RotateAround(tiltPivot, _lastTiltAxis, _currentTilt);
            }

            if (clampTipAboveWater && tipPivot != null)
            {
                root.position = WaterCarryClamp.ClampRootSoTipAboveWater(
                    root, tipPivot, root.position, root.rotation, waterTipClearance, groundMask);
            }

            // #region agent log
            if (dbgIsWateringCan)
            {
                var externRotDelta = _dbgHasLastWritten
                    ? Quaternion.Angle(_dbgLastWrittenRotation, dbgEntryRot)
                    : 0f;
                var frameRotDelta = _dbgHasLastWritten
                    ? Quaternion.Angle(_dbgLastWrittenRotation, root.rotation)
                    : 0f;
                var appliedRotDelta = Quaternion.Angle(dbgEntryRot, root.rotation);
                var tiltRotDelta = Quaternion.Angle(dbgRotBeforeTilt, root.rotation);
                var posDelta = _dbgHasLastWritten
                    ? Vector3.Distance(_dbgLastWrittenPosition, root.position)
                    : 0f;
                var shouldLog = Time.frameCount % 5 == 0
                    || frameRotDelta > 2f
                    || externRotDelta > 0.5f
                    || tiltRotDelta > 1f;

                if (shouldLog)
                {
                    DebugAgentLog.Log(
                        externRotDelta > 0.5f ? "H-E" : (tiltRotDelta > 1f ? "H-A" : (dbgPitchDelta > 0.5f ? "H-B" : "H-D")),
                        "CarryableCarryDriver.cs:TryApplySmoothedCarry",
                        "watering-carry-frame",
                        "{\"runId\":\"post-fix-v3\",\"frame\":" + Time.frameCount +
                        ",\"externRotDelta\":" + externRotDelta.ToString("F2") +
                        ",\"appliedRotDelta\":" + appliedRotDelta.ToString("F2") +
                        ",\"frameRotDelta\":" + frameRotDelta.ToString("F2") +
                        ",\"tiltRotDelta\":" + tiltRotDelta.ToString("F2") +
                        ",\"pitchDelta\":" + dbgPitchDelta.ToString("F2") +
                        ",\"posDelta\":" + posDelta.ToString("F4") +
                        ",\"currentTilt\":" + _currentTilt.ToString("F2") +
                        ",\"workPose\":" + (_workPoseActive ? "true" : "false") +
                        ",\"targetPosGap\":" + Vector3.Distance(targetPosition, _smoothedPosition).ToString("F4") +
                        ",\"anchorDelta\":" + (_hasLastAimAnchor ? Vector3.Distance(_lastAimAnchor, targetPosition).ToString("F4") : "0") +
                        "}");
                }

                _dbgLastWrittenRotation = root.rotation;
                _dbgLastWrittenPosition = root.position;
                _dbgHasLastWritten = true;
            }
            // #endregion

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

            var carryUp = ResolveCarryUpVector(surfaceNormal);
            position = anchor + carryUp * height;

            CacheBindTipPose(root, tipPivot);
            rotation = ComputeStableCarryRotation(root, carryUp);

            if (clampTipAboveWater)
            {
                position = WaterCarryClamp.ClampRootSoTipAboveWater(
                    root, tipPivot, position, rotation, waterTipClearance, groundMask);
            }

            return true;
        }

        private void CacheBindTipPose(Transform root, Transform tipPivot)
        {
            if (_bindTipCached || root == null || tipPivot == null)
            {
                return;
            }

            var worldOffset = tipPivot.position - root.position;
            if (worldOffset.sqrMagnitude < 0.0001f)
            {
                return;
            }

            _bindLocalTipOffset = root.InverseTransformPoint(tipPivot.position);
            _bindLocalTipDirection = root.InverseTransformDirection(worldOffset.normalized);
            _bindTipCached = true;
        }

        private void ResolveCarryRotationModifier()
        {
            if (_carryRotationModifier != null)
            {
                return;
            }

            _carryRotationModifier = GetComponent<ICarryableCarryRotationModifier>()
                ?? GetComponentInChildren<ICarryableCarryRotationModifier>(true);
        }

        private Vector3 ResolveCarryUpVector(Vector3 surfaceNormal)
        {
            return lockWorldUpright ? Vector3.up : surfaceNormal;
        }

        private Quaternion ComputeStableCarryRotation(Transform root, Vector3 surfaceNormal)
        {
            ResolveCarryRotationModifier();
            if (lockWorldUpright
                || (_carryRotationModifier != null && _carryRotationModifier.UsesYawPitchCarry))
            {
                return ComputeYawOnlyCarryRotation(ResolveCarryUpVector(surfaceNormal));
            }

            var desiredTipDir = (-surfaceNormal).normalized;
            var bindTip = _bindLocalTipDirection.sqrMagnitude > 0.0001f
                ? _bindLocalTipDirection
                : tipForwardAxis.normalized;

            var referenceRotation = _carryRotationInitialized ? _smoothedCarryRotation : root.rotation;
            var forwardOnPlane = Vector3.ProjectOnPlane(referenceRotation * Vector3.forward, surfaceNormal);
            if (forwardOnPlane.sqrMagnitude < 0.0001f)
            {
                forwardOnPlane = Vector3.ProjectOnPlane(referenceRotation * Vector3.right, surfaceNormal);
            }

            if (forwardOnPlane.sqrMagnitude < 0.0001f)
            {
                forwardOnPlane = Vector3.ProjectOnPlane(transform.forward, surfaceNormal);
            }

            if (forwardOnPlane.sqrMagnitude < 0.0001f)
            {
                forwardOnPlane = Vector3.ProjectOnPlane(Vector3.forward, surfaceNormal);
            }

            if (forwardOnPlane.sqrMagnitude > 0.0001f)
            {
                forwardOnPlane.Normalize();
            }
            else
            {
                return root.rotation;
            }

            var frameRotation = Quaternion.LookRotation(forwardOnPlane, surfaceNormal);
            var worldTipFromFrame = frameRotation * bindTip;
            if (worldTipFromFrame.sqrMagnitude < 0.0001f)
            {
                return root.rotation;
            }

            worldTipFromFrame.Normalize();
            var tipCorrection = Quaternion.FromToRotation(worldTipFromFrame, desiredTipDir);
            return tipCorrection * frameRotation;
        }

        private Quaternion ComputeYawOnlyCarryRotation(Vector3 surfaceNormal)
        {
            var desiredYaw = _lastYawDirection;

            if (_hasLastAimAnchor)
            {
                var anchorFlat = new Vector3(_lastAimAnchor.x, 0f, _lastAimAnchor.z);
                if (_hasPrevAimAnchorFlat)
                {
                    var anchorMove = anchorFlat - _prevAimAnchorFlat;
                    if (anchorMove.sqrMagnitude >= workMinTurnSpeed * workMinTurnSpeed)
                    {
                        desiredYaw = anchorMove.normalized;
                    }
                    else
                    {
                        var cam = ResolveCamera();
                        if (cam != null)
                        {
                            var camForward = Vector3.ProjectOnPlane(cam.transform.forward, surfaceNormal);
                            if (camForward.sqrMagnitude >= workMinTurnSpeed * workMinTurnSpeed)
                            {
                                desiredYaw = camForward.normalized;
                            }
                        }
                    }
                }

                _prevAimAnchorFlat = anchorFlat;
                _hasPrevAimAnchorFlat = true;
            }

            _lastYawDirection = desiredYaw;

            var forwardOnPlane = Vector3.ProjectOnPlane(_lastYawDirection, surfaceNormal);
            if (forwardOnPlane.sqrMagnitude < 0.0001f)
            {
                forwardOnPlane = Vector3.ProjectOnPlane(transform.forward, surfaceNormal);
            }

            if (forwardOnPlane.sqrMagnitude < 0.0001f)
            {
                return _carryRotationInitialized ? _smoothedCarryRotation : transform.rotation;
            }

            forwardOnPlane.Normalize();
            return Quaternion.LookRotation(forwardOnPlane, surfaceNormal);
        }

        private Quaternion ApplyModifierPitch(Quaternion yawRotation)
        {
            ResolveCarryRotationModifier();
            return _carryRotationModifier != null && _carryRotationModifier.UsesYawPitchCarry
                ? _carryRotationModifier.ApplyCarryRotationOffset(yawRotation)
                : yawRotation;
        }

        private Quaternion SmoothCarryRotation(Quaternion targetRotation, float deltaTime)
        {
            if (!_carryRotationInitialized)
            {
                _smoothedCarryRotation = targetRotation;
                _carryRotationInitialized = true;
                return targetRotation;
            }

            var t = 1f - Mathf.Exp(-carryTurnLerpSpeed * deltaTime);
            _smoothedCarryRotation = Quaternion.Slerp(_smoothedCarryRotation, targetRotation, t);
            return _smoothedCarryRotation;
        }

        private Quaternion ComputeWorkRotation(
            Quaternion carryRotation,
            Transform tipPivot,
            Vector3 horizontalVelocity,
            Vector3 surfaceNormal,
            float deltaTime)
        {
            var targetRotation = workPoseOrientation == WorkPoseOrientation.TipDownYaw
                ? ComputeTipDownYawWorkRotation(carryRotation, tipPivot, horizontalVelocity, surfaceNormal)
                : ComputeSlideOnSurfaceWorkRotation(tipPivot, horizontalVelocity, surfaceNormal);

            if (_workSmoothedRotation == Quaternion.identity)
            {
                _workSmoothedRotation = carryRotation;
            }

            var t = 1f - Mathf.Exp(-workTurnLerpSpeed * deltaTime);
            _workSmoothedRotation = Quaternion.Slerp(_workSmoothedRotation, targetRotation, t);
            return _workSmoothedRotation;
        }

        private Quaternion ComputeSlideOnSurfaceWorkRotation(
            Transform tipPivot,
            Vector3 horizontalVelocity,
            Vector3 surfaceNormal)
        {
            var desiredMoveDir = ResolveWorkFacingDirection(horizontalVelocity, surfaceNormal);

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

        private Vector3 ResolveWorkFacingDirection(
            Vector3 horizontalVelocity,
            Vector3 surfaceNormal)
        {
            var desiredMoveDir = _lastWorkMoveDir;
            if (horizontalVelocity.sqrMagnitude >= workMinTurnSpeed * workMinTurnSpeed)
            {
                desiredMoveDir = horizontalVelocity.normalized;
            }
            else if (_hasLastAimAnchor)
            {
                var toCursor = _lastAimAnchor - transform.position;
                var toCursorOnPlane = Vector3.ProjectOnPlane(toCursor, surfaceNormal);
                if (toCursorOnPlane.sqrMagnitude >= workMinTurnSpeed * workMinTurnSpeed)
                {
                    desiredMoveDir = toCursorOnPlane.normalized;
                }
            }

            _lastWorkMoveDir = desiredMoveDir;
            return desiredMoveDir;
        }

        private Quaternion ComputeTipDownYawWorkRotation(
            Quaternion carryRotation,
            Transform tipPivot,
            Vector3 horizontalVelocity,
            Vector3 surfaceNormal)
        {
            var desiredMoveDir = ResolveWorkFacingDirection(horizontalVelocity, surfaceNormal);

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
                RememberAimAnchor(anchor);
                return true;
            }

            if (!CarryableMouseRay.TryGetRay(cam, out var ray))
            {
                _hasLastAimAnchor = false;
                return false;
            }

            if (!TryRaycastAimSurface(ray, out anchor, out surfaceNormal)
                && !TrySampleWaterAlongRay(ray, out anchor, out surfaceNormal))
            {
                _hasLastAimAnchor = false;
                return false;
            }

            if (TryApplyWaterSurfaceAnchor(ref anchor, ref surfaceNormal))
            {
                RememberAimAnchor(anchor);
                return true;
            }

            FlattenAnchorToTopGround(ref anchor, ref surfaceNormal);
            RememberAimAnchor(anchor);
            return true;
        }

        private void RememberAimAnchor(Vector3 anchor)
        {
            if (!_smoothedAimInitialized)
            {
                _smoothedAimAnchor = anchor;
                _smoothedAimInitialized = true;
            }
            else
            {
                var smooth = Mathf.Max(aimAnchorSmoothTime, 0.01f);
                var t = 1f - Mathf.Exp(-Time.deltaTime / smooth);
                _smoothedAimAnchor = Vector3.Lerp(_smoothedAimAnchor, anchor, t);
            }

            _lastAimAnchor = _smoothedAimAnchor;
            _hasLastAimAnchor = true;
        }

        private void FlattenAnchorToTopGround(ref Vector3 anchor, ref Vector3 surfaceNormal)
        {
            if (!DecorationPlacementUtility.TrySampleTopGroundAt(
                    anchor, groundMask, null, out var downHit))
            {
                return;
            }

            anchor = new Vector3(anchor.x, downHit.point.y, anchor.z);
            surfaceNormal = Vector3.up;
        }

        private bool TryApplyWaterSurfaceAnchor(ref Vector3 anchor, ref Vector3 surfaceNormal)
        {
            if (!clampTipAboveWater)
            {
                return false;
            }

            var core = GetComponent<CarryableToolCore>();
            var tip = core != null ? core.Tip : null;
            if (tip == null
                || !WaterCarryClamp.ShouldUseWaterSurfaceAt(tip.position, groundMask, out _))
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
            if (tip == null
                || !WaterCarryClamp.ShouldUseWaterSurfaceAt(tip.position, groundMask, out _))
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
