using REHozy;
using REHozy.Audio;
using REHozy.CarryableTools;
using REHozy.Decoration;
using UnityEngine;

namespace REHozy.Watering
{
    [DisallowMultipleComponent]
    [AddComponentMenu("REHozy/Watering/Watering Can Tool Actions")]
    public sealed class WateringCanToolActions : MonoBehaviour, ICarryableToolActions, ICarryableToolCarriedUpdate,
        ICarryableWorkMagnet, ICarryableToolForceRelease
    {
        [Header("Pour")]
        [SerializeField] private WateringCanAimPivot aimPivot;
        [SerializeField] private ParticleSystem waterParticles;
        [SerializeField] private Vector3 pourDirectionLocal = Vector3.forward;
        [SerializeField] private float maxPourRayDistance = 5f;
        [SerializeField] private float wateringRadius = 0.75f;
        [SerializeField] private float waterAmountPerSecond = 1f;
        [SerializeField] private LayerMask waterableMask = ~0;

        [Header("Spout magnet (LMB only)")]
        [SerializeField] private float magnetDetectRadius = 3f;
        [SerializeField] private LayerMask aimAssistGroundMask = ~0;

        private static readonly Collider[] OverlapBuffer = new Collider[24];
        private static readonly RaycastHit[] PourRayHits = new RaycastHit[16];

        private bool _pourMagnetEnabled;
        private bool _wasPouring;
        private IWaterable _lockedMagnetTarget;

        private void Reset()
        {
            aimPivot = GetComponentInChildren<WateringCanAimPivot>(true);
            waterParticles = GetComponentInChildren<ParticleSystem>(true);
        }

        public bool HasCargo(CarryableToolCore tool) => false;

        public bool CanReturnHome(CarryableToolCore tool) => true;

        public bool OnCarriedClick(CarryableToolCore tool) => false;

        public void OnReturnHoldStartedInHome(CarryableToolCore tool)
        {
        }

        public void OnHoldCompleteInHome(CarryableToolCore tool)
        {
            tool.StartReturnHome();
        }

        public void OnHoldCompleteOutsideHome(CarryableToolCore tool)
        {
        }

        public bool TryGetWorkMagnet(
            UnityEngine.Camera camera,
            Transform tipPivot,
            Vector3 cursorGroundAnchor,
            out Vector3 targetWorldPoint,
            out float strength01)
        {
            if (!_pourMagnetEnabled || tipPivot == null)
            {
                targetWorldPoint = default;
                strength01 = 0f;
                return false;
            }

            return TryResolveSpoutMagnet(tipPivot, out targetWorldPoint, out strength01);
        }

        public void OnForceReleased(CarryableToolCore tool)
        {
            CancelPourWork(tool);
        }

        public void OnCarriedUpdate(CarryableToolCore tool, bool attackHeld, bool returnHoldInProgress)
        {
            if (tool.State != CarryableToolState.Carried)
            {
                CancelPourWork(tool);
                return;
            }

            var pouring = attackHeld && !returnHoldInProgress;

            if (pouring && !_wasPouring)
            {
                TryLockMagnetTarget(tool);
                StartPourAudio(tool);
            }
            else if (!pouring && _wasPouring)
            {
                ClearMagnetLock();
                StopPourAudio();
            }
            else if (pouring)
            {
                UpdatePourAudioPosition(tool);
            }

            _wasPouring = pouring;
            // Pour tilt is handled by WateringCanAimPivot; work pose adds extra yaw/height and causes jitter.
            tool.CarryDriver?.SetWorkPoseActive(false);
            aimPivot?.UpdatePourTilt(pouring, Time.deltaTime);

            // #region agent log
            if (pouring && Time.frameCount % 10 == 0)
            {
                DebugAgentLog.Log(
                    "H-C",
                    "WateringCanToolActions.cs:OnCarriedUpdate",
                    "pour-state",
                    "{\"frame\":" + Time.frameCount +
                    ",\"magnetEnabled\":" + (_pourMagnetEnabled ? "true" : "false") +
                    ",\"hasMagnetLock\":" + (HasActiveMagnetLock() ? "true" : "false") +
                    ",\"workPoseDriver\":\"" + (tool.CarryDriver != null ? tool.CarryDriver.GetType().Name : "null") + "\"" +
                    "}");
            }
            // #endregion

            if (!pouring)
            {
                StopPouring();
                return;
            }

            SetParticlesPlaying(true);

            if (!TryGetPourImpactPoint(tool, out var impactPoint))
            {
                return;
            }

            WaterAtPoint(impactPoint, Time.deltaTime);
        }

        private bool TryResolveSpoutMagnet(
            Transform tipPivot,
            out Vector3 targetWorldPoint,
            out float strength01)
        {
            targetWorldPoint = default;
            strength01 = 0f;

            if (!HasActiveMagnetLock())
            {
                return false;
            }

            if (!TryGetLockedMagnetAnchor(out targetWorldPoint))
            {
                ClearMagnetLock();
                return false;
            }

            strength01 = 1f;
            return true;
        }

        private bool HasActiveMagnetLock()
        {
            return _lockedMagnetTarget != null && !_lockedMagnetTarget.IsWateringComplete;
        }

        private void TryLockMagnetTarget(CarryableToolCore tool)
        {
            ClearMagnetLock();

            var spout = ResolveSpoutTransform(tool);
            if (spout == null)
            {
                return;
            }

            if (!TryFindNearestIncompleteWaterable(
                    spout.position,
                    out var waterable,
                    out _,
                    out _))
            {
                return;
            }

            _lockedMagnetTarget = waterable;
        }

        private void ClearMagnetLock()
        {
            _lockedMagnetTarget = null;
        }

        private bool TryGetLockedMagnetAnchor(out Vector3 anchor)
        {
            anchor = default;

            if (_lockedMagnetTarget == null || _lockedMagnetTarget.IsWateringComplete)
            {
                return false;
            }

            if (_lockedMagnetTarget is not Component behaviour)
            {
                return false;
            }

            return TryGetWaterableTargetPoints(behaviour, out anchor, out _);
        }

        private void CancelPourWork(CarryableToolCore tool)
        {
            aimPivot?.ResetPourTilt();
            StopPourAudio();
            StopPouring();
            ClearMagnetLock();
            _wasPouring = false;
            tool.CarryDriver?.SetWorkPoseActive(false);
        }

        private void StopPouring()
        {
            SetParticlesPlaying(false);
        }

        private void StartPourAudio(CarryableToolCore tool)
        {
            var spout = ResolveSpoutTransform(tool);
            if (spout == null)
            {
                return;
            }

            GameAudio.StartLoop(GameSoundId.WaterPourLoop, spout.position);
        }

        private void UpdatePourAudioPosition(CarryableToolCore tool)
        {
            var spout = ResolveSpoutTransform(tool);
            if (spout == null)
            {
                return;
            }

            GameAudio.StartLoop(GameSoundId.WaterPourLoop, spout.position);
        }

        private static void StopPourAudio()
        {
            GameAudio.StopLoop(GameSoundId.WaterPourLoop);
        }

        private void SetParticlesPlaying(bool playing)
        {
            if (waterParticles == null)
            {
                return;
            }

            if (playing)
            {
                if (!waterParticles.isPlaying)
                {
                    waterParticles.Play();
                }
            }
            else if (waterParticles.isPlaying)
            {
                waterParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        private bool TryGetPourImpactPoint(CarryableToolCore tool, out Vector3 impactPoint)
        {
            impactPoint = default;

            if (_pourMagnetEnabled && TryGetLockedMagnetAnchor(out impactPoint))
            {
                return true;
            }

            if (!TryGetPourRay(tool, out var origin, out var direction))
            {
                return tool.CarryDriver != null && tool.CarryDriver.TryGetGroundAnchor(out impactPoint);
            }

            var groundMask = tool.CarryDriver != null ? tool.CarryDriver.GroundMask : waterableMask;
            var maxDistance = Mathf.Max(maxPourRayDistance, 0.1f);

            var hitCount = Physics.RaycastNonAlloc(
                origin,
                direction,
                PourRayHits,
                maxDistance,
                groundMask,
                QueryTriggerInteraction.Ignore);

            var bestDistance = float.PositiveInfinity;
            var foundHit = false;

            for (var i = 0; i < hitCount; i++)
            {
                var hit = PourRayHits[i];
                if (hit.collider == null || IsToolCollider(hit.collider, tool))
                {
                    continue;
                }

                if (hit.distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = hit.distance;
                impactPoint = hit.point;
                foundHit = true;
            }

            if (foundHit)
            {
                return true;
            }

            if (TryGetGroundBelow(origin, groundMask, out impactPoint))
            {
                return true;
            }

            return tool.CarryDriver != null && tool.CarryDriver.TryGetGroundAnchor(out impactPoint);
        }

        private bool TryFindNearestIncompleteWaterable(
            Vector3 searchOrigin,
            out IWaterable waterable,
            out Vector3 targetCenter,
            out Vector3 targetGround)
        {
            waterable = null;
            targetCenter = default;
            targetGround = default;

            var radius = Mathf.Max(magnetDetectRadius, 0.1f);
            var hitCount = Physics.OverlapSphereNonAlloc(
                searchOrigin,
                radius,
                OverlapBuffer,
                waterableMask,
                QueryTriggerInteraction.Collide);

            var bestDistanceSq = float.PositiveInfinity;
            IWaterable best = null;
            var bestCenter = default(Vector3);
            var bestGround = default(Vector3);

            for (var i = 0; i < hitCount; i++)
            {
                var col = OverlapBuffer[i];
                if (col == null || IsOwnCollider(col))
                {
                    continue;
                }

                var candidate = col.GetComponentInParent<IWaterable>();
                if (candidate == null || candidate.IsWateringComplete)
                {
                    continue;
                }

                if (candidate is not Component behaviour)
                {
                    continue;
                }

                if (!TryGetWaterableTargetPoints(behaviour, out var center, out var ground))
                {
                    continue;
                }

                var delta = center - searchOrigin;
                delta.y = 0f;
                var distanceSq = delta.sqrMagnitude;
                if (distanceSq >= bestDistanceSq)
                {
                    continue;
                }

                bestDistanceSq = distanceSq;
                best = candidate;
                bestCenter = center;
                bestGround = ground;
            }

            if (best == null)
            {
                return false;
            }

            waterable = best;
            targetCenter = bestCenter;
            targetGround = bestGround;
            return true;
        }

        private bool TryGetWaterableTargetPoints(Component behaviour, out Vector3 center, out Vector3 ground)
        {
            center = behaviour.transform.position;
            ground = center;

            var colliders = behaviour.GetComponentsInChildren<Collider>();
            if (colliders.Length > 0)
            {
                var bounds = colliders[0].bounds;
                for (var i = 1; i < colliders.Length; i++)
                {
                    if (colliders[i] != null)
                    {
                        bounds.Encapsulate(colliders[i].bounds);
                    }
                }

                center = bounds.center;
                ground = new Vector3(center.x, bounds.min.y, center.z);
            }

            if (DecorationPlacementUtility.TrySampleTopGroundAt(
                    ground,
                    aimAssistGroundMask,
                    transform,
                    out var hit))
            {
                ground = new Vector3(ground.x, hit.point.y, ground.z);
            }

            return true;
        }

        private bool IsOwnCollider(Collider col)
        {
            if (col == null)
            {
                return false;
            }

            var hitTransform = col.transform;
            return hitTransform == transform || hitTransform.IsChildOf(transform);
        }

        private Transform ResolveSpoutTransform(CarryableToolCore tool)
        {
            if (waterParticles != null)
            {
                return waterParticles.transform;
            }

            if (aimPivot != null)
            {
                return aimPivot.Tip;
            }

            return tool.Tip;
        }

        private bool TryGetPourRay(CarryableToolCore tool, out Vector3 origin, out Vector3 direction)
        {
            origin = default;
            direction = default;

            var spout = ResolveSpoutTransform(tool);
            if (spout == null)
            {
                return false;
            }

            origin = spout.position;
            direction = spout.TransformDirection(pourDirectionLocal);
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = Vector3.down;
            }
            else
            {
                direction.Normalize();
            }

            return true;
        }

        private static bool TryGetGroundBelow(Vector3 from, LayerMask mask, out Vector3 groundPoint)
        {
            groundPoint = default;
            const float rayStartHeight = 50f;
            var probeOrigin = new Vector3(from.x, from.y + rayStartHeight, from.z);
            var maxDistance = rayStartHeight + Mathf.Max(from.y, 0f) + 10f;

            if (!Physics.Raycast(
                    probeOrigin,
                    Vector3.down,
                    out var hit,
                    maxDistance,
                    mask,
                    QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            groundPoint = hit.point;
            return true;
        }

        private static bool IsToolCollider(Collider col, CarryableToolCore tool)
        {
            if (col == null || tool == null)
            {
                return false;
            }

            var hitTransform = col.transform;
            return hitTransform == tool.transform || hitTransform.IsChildOf(tool.transform);
        }

        private void WaterAtPoint(Vector3 anchor, float deltaTime)
        {
            var hitCount = Physics.OverlapSphereNonAlloc(
                anchor,
                wateringRadius,
                OverlapBuffer,
                waterableMask,
                QueryTriggerInteraction.Collide);

            for (var i = 0; i < hitCount; i++)
            {
                var col = OverlapBuffer[i];
                if (col == null)
                {
                    continue;
                }

                var waterable = col.GetComponentInParent<IWaterable>();
                if (waterable == null || waterable.IsWateringComplete)
                {
                    continue;
                }

                waterable.TryWater(anchor, waterAmountPerSecond, deltaTime);
            }
        }
    }
}
