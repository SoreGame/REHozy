using UnityEngine;

namespace REHozy.CarryableTools
{
    [DisallowMultipleComponent]
    [AddComponentMenu("REHozy/Carryable Tools/Home Zone Registry")]
    public sealed class HomeZoneRegistry : MonoBehaviour
    {
        [SerializeField] private Collider homeZone;

        public static HomeZoneRegistry Instance { get; private set; }

        public Collider HomeZone => homeZone;

        private void Awake()
        {
            if (homeZone == null)
            {
                homeZone = GetComponent<Collider>();
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Reset()
        {
            homeZone = GetComponent<Collider>();
            if (homeZone != null)
            {
                homeZone.isTrigger = true;
            }
        }
    }
}
