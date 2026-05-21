using UnityEngine;

namespace REHozy.Harpoon
{
    /// <summary>
    /// Trash-bin consume only: scale to zero and destroy. Use <see cref="BeginTrashConsume"/> after <see cref="HarpoonMountableItem.ConsumeInTrashBin"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [AddComponentMenu("REHozy/Harpoon/Cargo Trash Drop")]
    public sealed class HarpoonCargoTrashDrop : MonoBehaviour
    {
        [SerializeField] private float shrinkDuration = 0.35f;

        private Rigidbody _rigidbody;
        private Vector3 _initialScale;
        private float _shrinkT;

        public void BeginTrashConsume()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _initialScale = transform.localScale;
            FreezePhysics();
            _shrinkT = 0f;
            enabled = true;
        }

        private void Update()
        {
            TickShrink();
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

    }
}
