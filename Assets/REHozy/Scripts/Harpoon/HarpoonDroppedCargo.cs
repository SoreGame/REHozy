using Bitgem.VFX.StylisedWater;
using REHozy;
using UnityEngine;

namespace REHozy.Harpoon
{
    /// <summary>
    /// Physics drop after release from harpoon: rests on ground or bobs on water via <see cref="WateverVolumeFloater"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [AddComponentMenu("REHozy/Harpoon/Dropped Cargo")]
    public sealed class HarpoonDroppedCargo : MonoBehaviour
    {
        private enum Phase
        {
            Dynamic,
            Grounded,
            Floating
        }

        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField] private LayerMask waterMask;
        [SerializeField] private float settleSpeedThreshold = 0.35f;
        [SerializeField] private float groundProbeDistance = 0.5f;
        [SerializeField] private float waterSurfaceThreshold = 0.25f;

        private Rigidbody _rigidbody;
        private Phase _phase = Phase.Dynamic;

        public void Initialize(LayerMask ground, LayerMask water)
        {
            groundMask = ground;
            if (water.value != 0)
            {
                waterMask = water;
            }

            ResetForNewDrop();
        }

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
        }

        private void ResetForNewDrop()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _phase = Phase.Dynamic;

            foreach (var floater in GetComponentsInChildren<WateverVolumeFloater>(true))
            {
                floater.enabled = false;
            }

            foreach (var wander in GetComponentsInChildren<FloatingTargetWander>(true))
            {
                wander.enabled = false;
            }
        }

        private void FixedUpdate()
        {
            if (_rigidbody == null)
            {
                return;
            }

            if (_phase == Phase.Floating)
            {
                return;
            }

            if (ShouldEnterWater())
            {
                EnterWaterFloating();
                return;
            }

            if (_phase != Phase.Dynamic)
            {
                return;
            }

            if (_rigidbody.linearVelocity.sqrMagnitude > settleSpeedThreshold * settleSpeedThreshold)
            {
                return;
            }

            if (IsInWaterVolumeAtXz())
            {
                return;
            }

            if (IsGroundBelow())
            {
                EnterGrounded();
            }
        }

        private void Update()
        {
            if (_phase != Phase.Grounded)
            {
                return;
            }

            if (ShouldEnterWater())
            {
                EnterWaterFloating();
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_phase == Phase.Floating || collision.collider == null)
            {
                return;
            }

            if (IsWaterCollider(collision.collider) || ShouldEnterWater())
            {
                EnterWaterFloating();
                return;
            }

            if (_phase == Phase.Dynamic
                && IsGroundCollider(collision.collider)
                && !IsInWaterVolumeAtXz())
            {
                TryEnterGroundedWhenSlow();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_phase == Phase.Floating || other == null)
            {
                return;
            }

            if (IsWaterCollider(other) || ShouldEnterWater())
            {
                EnterWaterFloating();
            }
        }

        private void OnTriggerStay(Collider other)
        {
            if (_phase == Phase.Grounded && other != null && ShouldEnterWater())
            {
                EnterWaterFloating();
            }
        }

        private void TryEnterGroundedWhenSlow()
        {
            if (_rigidbody == null
                || _rigidbody.linearVelocity.sqrMagnitude > settleSpeedThreshold * settleSpeedThreshold)
            {
                return;
            }

            EnterGrounded();
        }

        private bool ShouldEnterWater()
        {
            if (!TryGetWaterSurfaceY(out var waterY))
            {
                return false;
            }

            return GetSubmergeSampleY() <= waterY + waterSurfaceThreshold;
        }

        private bool IsInWaterVolumeAtXz()
        {
            return TryGetWaterSurfaceY(out _);
        }

        private bool TryGetWaterSurfaceY(out float waterY)
        {
            waterY = 0f;
            var helper = WaterVolumeHelper.Instance;
            if (helper == null)
            {
                return false;
            }

            var height = helper.GetHeight(transform.position);
            if (height == null)
            {
                return false;
            }

            waterY = height.Value;
            return true;
        }

        private float GetSubmergeSampleY()
        {
            var col = GetComponentInChildren<Collider>();
            if (col != null && col.enabled)
            {
                return col.bounds.min.y;
            }

            return transform.position.y;
        }

        private void EnterWaterFloating()
        {
            if (_phase == Phase.Floating)
            {
                return;
            }

            _phase = Phase.Floating;

            if (_rigidbody != null)
            {
                _rigidbody.linearVelocity = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;
                _rigidbody.useGravity = false;
                _rigidbody.isKinematic = true;
            }

            var helper = WaterVolumeHelper.Instance;
            foreach (var floater in GetComponentsInChildren<WateverVolumeFloater>(true))
            {
                if (helper != null && floater.WaterVolumeHelper == null)
                {
                    floater.WaterVolumeHelper = helper;
                }

                floater.enabled = true;
            }

            foreach (var wander in GetComponentsInChildren<FloatingTargetWander>(true))
            {
                wander.enabled = true;
            }
        }

        private void EnterGrounded()
        {
            if (_phase != Phase.Dynamic || IsInWaterVolumeAtXz())
            {
                return;
            }

            _phase = Phase.Grounded;

            if (_rigidbody != null)
            {
                _rigidbody.linearVelocity = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;
                _rigidbody.isKinematic = true;
            }
        }

        private bool IsGroundBelow()
        {
            var origin = transform.position + Vector3.up * 0.05f;
            return Physics.Raycast(origin, Vector3.down, groundProbeDistance, groundMask,
                QueryTriggerInteraction.Ignore);
        }

        private bool IsGroundCollider(Collider col) =>
            ((1 << col.gameObject.layer) & groundMask.value) != 0 && !IsWaterCollider(col);

        private bool IsWaterCollider(Collider col) =>
            waterMask.value != 0 && ((1 << col.gameObject.layer) & waterMask.value) != 0;
    }
}
