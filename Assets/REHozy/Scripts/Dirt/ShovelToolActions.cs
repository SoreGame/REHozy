using REHozy.CarryableTools;
using UnityEngine;

namespace REHozy.Dirt
{
    [DisallowMultipleComponent]
    [AddComponentMenu("REHozy/Dirt/Shovel Tool Actions")]
    public sealed class ShovelToolActions : MonoBehaviour, ICarryableToolActions, ICarryableToolCarriedUpdate
    {
        [Header("Digging")]
        [SerializeField] private float digRadius = 0.45f;
        [SerializeField] private float digStrength = 1.25f;
        [SerializeField] private LayerMask dirtPatchMask = ~0;

        private static readonly Collider[] OverlapBuffer = new Collider[16];

        public bool HasCargo(CarryableToolCore tool) => false;

        public bool CanReturnHome(CarryableToolCore tool) => tool.State == CarryableToolState.Carried;

        public bool OnCarriedClick(CarryableToolCore tool) => false;

        public void OnHoldCompleteInHome(CarryableToolCore tool)
        {
            tool.StartReturnHome();
        }

        public void OnHoldCompleteOutsideHome(CarryableToolCore tool)
        {
        }

        public void OnCarriedUpdate(CarryableToolCore tool, bool attackHeld, bool returnHoldInProgress)
        {
            if (!attackHeld || returnHoldInProgress || tool.State != CarryableToolState.Carried)
            {
                return;
            }

            DigAtTip(tool);
        }

        private void DigAtTip(CarryableToolCore tool)
        {
            var tipPosition = tool.Tip.position;
            var hitCount = Physics.OverlapSphereNonAlloc(
                tipPosition,
                digRadius,
                OverlapBuffer,
                dirtPatchMask,
                QueryTriggerInteraction.Ignore);

            for (var i = 0; i < hitCount; i++)
            {
                var collider = OverlapBuffer[i];
                if (collider == null)
                {
                    continue;
                }

                var patch = collider.GetComponentInParent<DirtDeformPatch>();
                if (patch == null)
                {
                    continue;
                }

                patch.TryErodeAtWorld(tipPosition, digRadius, digStrength);
            }
        }
    }
}
