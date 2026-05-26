using System.Collections;
using REHozy.CarryableTools;
using UnityEngine;
using UnityEngine.Events;

namespace REHozy.Harpoon
{
    [DisallowMultipleComponent]
    [AddComponentMenu("REHozy/Harpoon/Harpoon Tool Actions")]
    public sealed class HarpoonToolActions : MonoBehaviour, ICarryableToolActions
    {
        [Header("Harpoon interaction")]
        [SerializeField] private float impaleRadius = 0.35f;
        [SerializeField] private LayerMask mountableMask = ~0;

        [Header("Impale strike")]
        [Tooltip("How far the harpoon moves along tip direction during capture.")]
        [SerializeField] private float impaleStrikeDistance = 0.35f;
        [Tooltip("If enabled, strike distance scales with current harpoon length (root → Tip).")]
        [SerializeField] private bool scaleStrikeWithTipDistance = true;
        [Tooltip("Strike distance = max(impaleStrikeDistance, tipDistance * this).")]
        [SerializeField, Range(0.01f, 0.8f)] private float impaleStrikeTipDistanceMultiplier = 0.12f;
        [Tooltip("Optional clamp for scaled strike distance (0 = no clamp).")]
        [SerializeField] private float maxImpaleStrikeDistance = 0f;

        [Tooltip("Fraction of Animation Lock Duration used for the fast downward stroke.")]
        [SerializeField, Range(0.05f, 0.5f)] private float impaleStrikeDownFraction = 0.2f;

        [Header("Strike clamp")]
        [Tooltip("If enabled, prevents the strike from pushing the tip below water/ground clearance.")]
        [SerializeField] private bool clampStrikeAboveWater = false;
        [Tooltip("Clearance used during strike when clamping is enabled.")]
        [SerializeField] private float strikeTipClearance = 0.02f;

        [Header("Events")]
        [SerializeField] private UnityEvent onImpaleStarted;
        [SerializeField] private UnityEvent onImpaleFinished;
        [SerializeField] private UnityEvent onDisposeStarted;
        [SerializeField] private UnityEvent onDisposeFinished;
        [SerializeField] private UnityEvent onBlockedReturnStarted;
        [SerializeField] private UnityEvent onBlockedReturnFinished;

        private CarryableToolCore _core;
        private HarpoonMountableItem _mountedItem;

        private void Awake()
        {
            _core = GetComponent<CarryableToolCore>();
        }

        public bool HasCargo(CarryableToolCore tool) => _mountedItem != null;

        public bool CanReturnHome(CarryableToolCore tool) => _mountedItem == null && tool.IsInHomeZone();

        public bool OnCarriedClick(CarryableToolCore tool)
        {
            if (TryDisposeOnClick(tool))
            {
                return true;
            }

            return TryImpaleOnClick(tool);
        }

        public void OnHoldCompleteInHome(CarryableToolCore tool)
        {
            if (HasCargo(tool))
            {
                tool.StartPhase(BlockedReturnPhase(), onBlockedReturnStarted, onBlockedReturnFinished);
            }
            else
            {
                tool.StartReturnHome();
            }
        }

        public void OnHoldCompleteOutsideHome(CarryableToolCore tool)
        {
        }

        private bool TryImpaleOnClick(CarryableToolCore tool)
        {
            if (tool.State != CarryableToolState.Carried || _mountedItem != null)
            {
                return false;
            }

            tool.StartPhase(ImpalePhase(tool), onImpaleStarted, onImpaleFinished);
            return true;
        }

        private bool TryDisposeOnClick(CarryableToolCore tool)
        {
            if (tool.State != CarryableToolState.Carried || _mountedItem == null)
            {
                return false;
            }

            if (IsOverTrashBin(tool))
            {
                tool.StartPhase(TrashConsumePhase(tool), onDisposeStarted, onDisposeFinished);
                return true;
            }

            tool.StartPhase(DropPhase(tool), onDisposeStarted, onDisposeFinished);
            return true;
        }

        private static bool IsOverTrashBin(CarryableToolCore tool)
        {
            if (FindTrashBinAt(tool.Tip.position) != null)
            {
                return true;
            }

            if (tool.CarryDriver != null && tool.CarryDriver.TryGetGroundAnchor(out var ground))
            {
                return FindTrashBinAt(ground) != null;
            }

            return false;
        }

        private IEnumerator ImpalePhase(CarryableToolCore tool)
        {
            yield return PlayImpaleStrike(tool, () =>
            {
                var item = FindMountableUnderTip(tool);
                if (item == null)
                {
                    return;
                }

                _mountedItem = item;
                item.OnMounted(tool.MountSocket);
                item.AlignToSocket(tool.MountSocket);
            });
        }

        private IEnumerator PlayImpaleStrike(CarryableToolCore tool, System.Action onStrikeBottom)
        {
            var duration = Mathf.Max(tool.AnimationLockDuration, 0.01f);
            var downDuration = duration * impaleStrikeDownFraction;
            var upDuration = duration - downDuration;

            var restPosition = tool.transform.position;
            var strikeDirection = GetStrikeDirection(tool);
            var dip = ResolveImpaleStrikeDistance(tool);

            var downElapsed = 0f;
            while (downElapsed < downDuration)
            {
                downElapsed += Time.deltaTime;
                var t = Mathf.Clamp01(downElapsed / Mathf.Max(downDuration, 0.0001f));
                var depth = EaseOutSharp(t);
                tool.transform.position = ApplyStrikeClamp(tool, restPosition + strikeDirection * (dip * depth));
                yield return null;
            }

            tool.transform.position = ApplyStrikeClamp(tool, restPosition + strikeDirection * dip);
            onStrikeBottom?.Invoke();

            var upElapsed = 0f;
            while (upElapsed < upDuration)
            {
                upElapsed += Time.deltaTime;
                var t = Mathf.Clamp01(upElapsed / Mathf.Max(upDuration, 0.0001f));
                var depth = 1f - EaseInGentle(t);
                tool.transform.position = ApplyStrikeClamp(tool, restPosition + strikeDirection * (dip * depth));
                yield return null;
            }

            tool.transform.position = restPosition;
            tool.CarryDriver?.ResetCarryMotion(restPosition);
        }

        private float ResolveImpaleStrikeDistance(CarryableToolCore tool)
        {
            var dip = Mathf.Max(0.001f, impaleStrikeDistance);
            if (!scaleStrikeWithTipDistance || tool == null || tool.Tip == null)
            {
                return dip;
            }

            var tipDistance = Vector3.Distance(tool.transform.position, tool.Tip.position);
            if (tipDistance > 0.0001f)
            {
                dip = Mathf.Max(dip, tipDistance * impaleStrikeTipDistanceMultiplier);
            }

            if (maxImpaleStrikeDistance > 0f)
            {
                dip = Mathf.Min(dip, maxImpaleStrikeDistance);
            }

            return Mathf.Max(0.001f, dip);
        }

        private Vector3 ApplyStrikeClamp(CarryableToolCore tool, Vector3 rootPosition)
        {
            if (!clampStrikeAboveWater || tool == null)
            {
                return rootPosition;
            }

            var groundMask = tool.CarryDriver != null ? tool.CarryDriver.GroundMask : (LayerMask)~0;
            return WaterCarryClamp.ClampRootSoTipAboveWater(
                tool.transform,
                tool.Tip,
                rootPosition,
                tool.transform.rotation,
                clearance: strikeTipClearance,
                groundMask: groundMask);
        }

        private static Vector3 ClampAboveWater(CarryableToolCore tool, Vector3 rootPosition)
        {
            var groundMask = tool.CarryDriver != null ? tool.CarryDriver.GroundMask : (LayerMask)~0;
            return WaterCarryClamp.ClampRootSoTipAboveWater(
                tool.transform,
                tool.Tip,
                rootPosition,
                tool.transform.rotation,
                groundMask: groundMask);
        }

        private static Vector3 GetStrikeDirection(CarryableToolCore tool)
        {
            var rootPosition = tool.transform.position;
            var tipOffset = tool.Tip.position - rootPosition;
            if (tipOffset.sqrMagnitude < 0.0001f)
            {
                return Vector3.down;
            }

            return tipOffset.normalized;
        }

        private static float EaseOutSharp(float t) => 1f - Mathf.Pow(1f - t, 3f);

        private static float EaseInGentle(float t) => t * t * (3f - 2f * t);

        private IEnumerator DropPhase(CarryableToolCore tool)
        {
            var item = _mountedItem;
            _mountedItem = null;
            yield return new WaitForSeconds(tool.AnimationLockDuration);

            if (item != null)
            {
                var groundMask = tool.CarryDriver != null ? tool.CarryDriver.GroundMask : (LayerMask)~0;
                item.ReleaseDropped(groundMask, ResolveWaterMask());
            }
        }

        private IEnumerator TrashConsumePhase(CarryableToolCore tool)
        {
            var item = _mountedItem;
            _mountedItem = null;
            yield return new WaitForSeconds(tool.AnimationLockDuration);

            item?.ConsumeInTrashBin();
        }

        private static LayerMask ResolveWaterMask()
        {
            var waterLayer = LayerMask.NameToLayer("Water");
            if (waterLayer < 0)
            {
                return default;
            }

            return 1 << waterLayer;
        }

        private IEnumerator BlockedReturnPhase()
        {
            yield return new WaitForSeconds(_core != null ? _core.AnimationLockDuration : 0.3f);
        }

        private HarpoonMountableItem FindMountableUnderTip(CarryableToolCore tool)
        {
            var best = default(HarpoonMountableItem);
            var bestDist = float.MaxValue;

            void ConsiderAt(Vector3 origin, float radius)
            {
                var hits = Physics.OverlapSphere(origin, radius, mountableMask, QueryTriggerInteraction.Collide);
                foreach (var hit in hits)
                {
                    var item = hit.GetComponentInParent<HarpoonMountableItem>();
                    if (item == null || !item.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    var dist = Vector3.SqrMagnitude(item.transform.position - origin);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        best = item;
                    }
                }
            }

            // Primary: at Tip (expected).
            var tipPos = tool.Tip.position;
            ConsiderAt(tipPos, impaleRadius);

            if (best != null)
            {
                return best;
            }

            // Fallback: sample along the tool axis (helps if Tip was moved accidentally after model swap).
            var rootPos = tool.transform.position;
            var midPos = Vector3.Lerp(rootPos, tipPos, 0.5f);
            ConsiderAt(midPos, impaleRadius);
            ConsiderAt(rootPos, impaleRadius * 0.75f);

            if (best != null)
            {
                return best;
            }

            // Last fallback: if we can resolve an anchor (ground/water) under the tool, try there too.
            if (tool.CarryDriver != null && tool.CarryDriver.TryGetGroundAnchor(out var anchor))
            {
                ConsiderAt(anchor, impaleRadius);
            }

            return best;
        }

        private static HarpoonTrashBin FindTrashBinAt(Vector3 position)
        {
            var bins = FindObjectsByType<HarpoonTrashBin>(FindObjectsSortMode.None);
            foreach (var bin in bins)
            {
                if (bin.Contains(position))
                {
                    return bin;
                }
            }

            return null;
        }
    }
}
