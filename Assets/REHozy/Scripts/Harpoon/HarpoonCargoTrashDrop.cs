using System.Collections;
using UnityEngine;

namespace REHozy.Harpoon
{
    /// <summary>
    /// Cargo released in trash area: falls, rests on the ground, then scales to zero and is destroyed.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [AddComponentMenu("REHozy/Harpoon/Cargo Trash Drop")]
    public sealed class HarpoonCargoTrashDrop : MonoBehaviour
    {
        private enum Phase
        {
            Falling,
            Lying,
            Shrinking
        }

        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField] private float lieOnGroundDuration = 1f;
        [SerializeField] private float shrinkDuration = 0.35f;
        [SerializeField] private float maxLifetime = 15f;
        [SerializeField] private float settleSpeedThreshold = 0.35f;
        [SerializeField] private float groundProbeDistance = 0.3f;

        private Rigidbody _rigidbody;
        private Phase _phase = Phase.Falling;
        private Vector3 _initialScale;
        private float _spawnTime;
        private float _shrinkT;

        public void Initialize(LayerMask mask)
        {
            groundMask = mask;
        }

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _initialScale = transform.localScale;
            _spawnTime = Time.time;
        }

        private void FixedUpdate()
        {
            if (_phase != Phase.Falling || _rigidbody == null)
            {
                return;
            }

            if (Time.time - _spawnTime > maxLifetime)
            {
                BeginShrink();
                return;
            }

            if (_rigidbody.linearVelocity.sqrMagnitude > settleSpeedThreshold * settleSpeedThreshold)
            {
                return;
            }

            if (IsGroundBelow())
            {
                BeginLieOnGround();
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_phase != Phase.Falling || collision.collider == null)
            {
                return;
            }

            if (IsGroundCollider(collision.collider))
            {
                BeginLieOnGround();
            }
        }

        private void Update()
        {
            if (_phase == Phase.Shrinking)
            {
                TickShrink();
            }
        }

        private void BeginLieOnGround()
        {
            if (_phase != Phase.Falling)
            {
                return;
            }

            _phase = Phase.Lying;
            FreezePhysics();
            StartCoroutine(LieThenShrinkRoutine());
        }

        private IEnumerator LieThenShrinkRoutine()
        {
            yield return new WaitForSeconds(lieOnGroundDuration);
            BeginShrink();
        }

        private void BeginShrink()
        {
            if (_phase == Phase.Shrinking)
            {
                return;
            }

            _phase = Phase.Shrinking;
            FreezePhysics();
            _shrinkT = 0f;
        }

        private void TickShrink()
        {
            _shrinkT += Time.deltaTime / Mathf.Max(shrinkDuration, 0.01f);
            var eased = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_shrinkT));
            transform.localScale = Vector3.Lerp(_initialScale, Vector3.zero, eased);

            if (_shrinkT >= 1f)
            {
                Destroy(gameObject);
            }
        }

        private void FreezePhysics()
        {
            if (_rigidbody == null)
            {
                return;
            }

            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
            _rigidbody.isKinematic = true;
        }

        private bool IsGroundBelow()
        {
            var origin = transform.position + Vector3.up * 0.05f;
            return Physics.Raycast(origin, Vector3.down, groundProbeDistance, groundMask,
                QueryTriggerInteraction.Ignore);
        }

        private bool IsGroundCollider(Collider col) =>
            ((1 << col.gameObject.layer) & groundMask.value) != 0;
    }
}
