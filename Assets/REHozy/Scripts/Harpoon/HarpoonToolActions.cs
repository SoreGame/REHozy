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

            var candidate = FindMountableUnderTip(tool);
            if (candidate == null)
            {
                return false;
            }

            tool.StartPhase(ImpalePhase(candidate, tool), onImpaleStarted, onImpaleFinished);
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

        private IEnumerator ImpalePhase(HarpoonMountableItem item, CarryableToolCore tool)
        {
            _mountedItem = item;
            item.OnMounted(tool.MountSocket);
            item.AlignToSocket(tool.MountSocket);
            yield return new WaitForSeconds(tool.AnimationLockDuration);
        }

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
            var origin = tool.Tip.position;
            var hits = Physics.OverlapSphere(origin, impaleRadius, mountableMask, QueryTriggerInteraction.Collide);
            HarpoonMountableItem best = null;
            var bestDist = float.MaxValue;

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
