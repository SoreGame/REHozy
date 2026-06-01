using REHozy.Audio;
using REHozy.CarryableTools;
using UnityEngine;

namespace REHozy.Dirt
{
    [DisallowMultipleComponent]
    [AddComponentMenu("REHozy/Dirt/Shovel Tool Actions")]
    public sealed class ShovelToolActions : MonoBehaviour, ICarryableToolActions, ICarryableToolCarriedUpdate,
        ICarryableAimOverride, ICarryableToolForceRelease
    {
        [Header("Digging")]
        [SerializeField] private float digRadius = 0.45f;
        [SerializeField] private float digStrength = 1.25f;
        [SerializeField] private LayerMask dirtPatchMask = ~0;
        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField] private float digRayDistance = 120f;

        private static readonly Collider[] OverlapBuffer = new Collider[16];
        private static readonly RaycastHit[] RaycastHits = new RaycastHit[32];
        private static readonly DirtDeformPatch[] PatchScratch = new DirtDeformPatch[8];

        private bool _digAimActive;
        private bool _digLoopActive;
        private Vector3 _lastDigAnchor;
        private bool _hasLastDigAnchor;

        /// <summary>All layers — dirt patches in scene may be on Default, not only DirtPatch layer.</summary>
        private const int DirtDiscoveryMask = ~0;

        public bool HasCargo(CarryableToolCore tool) => false;

        public bool CanReturnHome(CarryableToolCore tool) => tool.State == CarryableToolState.Carried;

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

        public bool TryOverrideAim(UnityEngine.Camera camera, out Vector3 anchor, out Vector3 planeNormal)
        {
            anchor = default;
            planeNormal = Vector3.up;

            if (!_digAimActive || camera == null)
            {
                return false;
            }

            if (!CarryableMouseRay.TryGetRay(camera, out var ray))
            {
                return false;
            }

            if (!TryFindClosestDigHit(ray, out var hit))
            {
                return false;
            }

            anchor = hit.point;
            planeNormal = hit.normal.sqrMagnitude > 0.0001f ? hit.normal : Vector3.up;
            _lastDigAnchor = anchor;
            _hasLastDigAnchor = true;
            return true;
        }

        public void OnForceReleased(CarryableToolCore tool)
        {
            CancelDigWork(tool);
        }

        public void OnCarriedUpdate(CarryableToolCore tool, bool attackHeld, bool returnHoldInProgress)
        {
            if (tool.State != CarryableToolState.Carried)
            {
                CancelDigWork(tool);
                return;
            }

            if (!attackHeld || returnHoldInProgress)
            {
                CancelDigWork(tool);
                return;
            }

            tool.CarryDriver?.SetWorkPoseActive(true);
            _digAimActive = true;
            StartDigAudio(tool.Tip.position);
            DigAtTip(tool);
        }

        private void CancelDigWork(CarryableToolCore tool)
        {
            tool.CarryDriver?.SetWorkPoseActive(false);
            ReleaseDigAim();
            StopDigAudio();
        }

        private void ReleaseDigAim()
        {
            _digAimActive = false;
            _hasLastDigAnchor = false;
        }

        private void StartDigAudio(Vector3 worldPosition)
        {
            if (!_digLoopActive)
            {
                _digLoopActive = true;
            }

            GameAudio.StartLoop(GameSoundId.ShovelDigLoop, worldPosition);
        }

        private void StopDigAudio()
        {
            if (!_digLoopActive)
            {
                return;
            }

            _digLoopActive = false;
            GameAudio.StopLoop(GameSoundId.ShovelDigLoop);
        }

        private void DigAtTip(CarryableToolCore tool)
        {
            var patchCount = 0;
            var digPoint = tool.Tip.position;

            var driver = tool.CarryDriver;
            var camera = driver != null ? driver.ResolveCameraForAim() : UnityEngine.Camera.main;
            if (camera != null && CarryableMouseRay.TryGetRay(camera, out var ray)
                && TryFindClosestDigHit(ray, out var mouseHit))
            {
                digPoint = mouseHit.point;
                TryAddPatch(mouseHit.collider.GetComponentInParent<DirtDeformPatch>(), ref patchCount);
            }

            if (_hasLastDigAnchor)
            {
                CollectDigPatches(_lastDigAnchor, ref patchCount);
            }

            CollectDigPatches(tool.Tip.position, ref patchCount);
            CollectDigPatches(Vector3.Lerp(tool.transform.position, tool.Tip.position, 0.5f), ref patchCount);

            for (var i = 0; i < patchCount; i++)
            {
                var patch = PatchScratch[i];
                if (patch == null)
                {
                    continue;
                }

                patch.TryErodeAtWorld(digPoint, digRadius, digStrength);
            }
        }

        private bool TryFindClosestDigHit(Ray ray, out RaycastHit bestHit)
        {
            bestHit = default;

            var hitCount = Physics.RaycastNonAlloc(
                ray,
                RaycastHits,
                digRayDistance,
                DirtDiscoveryMask,
                QueryTriggerInteraction.Ignore);

            if (hitCount <= 0)
            {
                return false;
            }

            var bestDirtDistance = float.MaxValue;
            var bestGroundDistance = float.MaxValue;
            var foundDirt = false;
            var foundGround = false;

            for (var i = 0; i < hitCount; i++)
            {
                var hit = RaycastHits[i];
                if (hit.collider == null || hit.collider.transform.IsChildOf(transform))
                {
                    continue;
                }

                if (WaterCarryClamp.IsWaterLayer(hit.collider.gameObject.layer))
                {
                    continue;
                }

                var patch = hit.collider.GetComponentInParent<DirtDeformPatch>();
                if (patch != null && hit.distance < bestDirtDistance)
                {
                    bestDirtDistance = hit.distance;
                    bestHit = hit;
                    foundDirt = true;
                    continue;
                }

                if (Vector3.Dot(hit.normal, Vector3.up) < 0.55f)
                {
                    continue;
                }

                if (hit.distance < bestGroundDistance)
                {
                    bestGroundDistance = hit.distance;
                    if (!foundDirt)
                    {
                        bestHit = hit;
                        foundGround = true;
                    }
                }
            }

            return foundDirt || foundGround;
        }

        private void CollectDigPatches(Vector3 probePoint, ref int patchCount)
        {
            CollectDigPatchesFromRaycast(probePoint, ref patchCount);

            var overlapCount = Physics.OverlapSphereNonAlloc(
                probePoint,
                digRadius,
                OverlapBuffer,
                DirtDiscoveryMask,
                QueryTriggerInteraction.Ignore);

            for (var i = 0; i < overlapCount; i++)
            {
                var collider = OverlapBuffer[i];
                if (collider == null)
                {
                    continue;
                }

                TryAddPatch(collider.GetComponentInParent<DirtDeformPatch>(), ref patchCount);
            }
        }

        private void CollectDigPatchesFromRaycast(Vector3 probePoint, ref int patchCount)
        {
            var origin = new Vector3(probePoint.x, probePoint.y + 50f, probePoint.z);
            var hitCount = Physics.RaycastNonAlloc(
                origin,
                Vector3.down,
                RaycastHits,
                120f,
                DirtDiscoveryMask,
                QueryTriggerInteraction.Ignore);

            for (var i = 0; i < hitCount; i++)
            {
                var hit = RaycastHits[i];
                if (hit.collider == null)
                {
                    continue;
                }

                TryAddPatch(hit.collider.GetComponentInParent<DirtDeformPatch>(), ref patchCount);
            }
        }

        private static void TryAddPatch(DirtDeformPatch patch, ref int patchCount)
        {
            if (patch == null || patchCount >= PatchScratch.Length)
            {
                return;
            }

            for (var i = 0; i < patchCount; i++)
            {
                if (PatchScratch[i] == patch)
                {
                    return;
                }
            }

            PatchScratch[patchCount++] = patch;
        }
    }
}
