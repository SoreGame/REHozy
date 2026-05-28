using REHozy.CarryableTools;
using UnityEngine;

namespace REHozy.Watering
{
    [DisallowMultipleComponent]
    [AddComponentMenu("REHozy/Watering/Watering Can Tool Actions")]
    public sealed class WateringCanToolActions : MonoBehaviour, ICarryableToolActions, ICarryableToolCarriedUpdate
    {
        [Header("Pour")]
        [SerializeField] private WateringCanAimPivot aimPivot;
        [SerializeField] private ParticleSystem waterParticles;
        [SerializeField] private WateringAreaIndicator areaIndicator;
        [SerializeField] private float wateringRadius = 0.75f;
        [SerializeField] private float waterAmountPerSecond = 1f;
        [SerializeField] private LayerMask waterableMask = ~0;

        private static readonly Collider[] OverlapBuffer = new Collider[24];

        private void Reset()
        {
            aimPivot = GetComponentInChildren<WateringCanAimPivot>(true);
            waterParticles = GetComponentInChildren<ParticleSystem>(true);
            areaIndicator = GetComponentInChildren<WateringAreaIndicator>(true);
        }

        public bool HasCargo(CarryableToolCore tool) => false;

        public bool CanReturnHome(CarryableToolCore tool) => true;

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
            if (tool.State != CarryableToolState.Carried)
            {
                StopPouring();
                return;
            }

            var pouring = attackHeld && !returnHoldInProgress;
            tool.CarryDriver?.SetWorkPoseActive(pouring);
            aimPivot?.UpdatePourTilt(pouring, Time.deltaTime);

            if (!pouring)
            {
                StopPouring();
                return;
            }

            SetParticlesPlaying(true);
            areaIndicator?.SetVisible(true);
            if (areaIndicator != null)
            {
                areaIndicator.Radius = wateringRadius;
            }

            if (!tool.CarryDriver.TryGetGroundAnchor(out var anchor))
            {
                return;
            }

            WaterAtPoint(anchor, Time.deltaTime);
        }

        private void StopPouring()
        {
            aimPivot?.ResetPourTilt();
            SetParticlesPlaying(false);
            areaIndicator?.SetVisible(false);
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
                waterable?.TryWater(anchor, waterAmountPerSecond, deltaTime);
            }
        }
    }
}
