using System.Collections.Generic;
using REHozy.Decoration;
using REHozy.Rendering;
using UnityEngine;

namespace REHozy.Torch
{
    [DisallowMultipleComponent]
    [AddComponentMenu("REHozy/Torch/Static Torch")]
    public sealed class StaticTorch : MonoBehaviour
    {
        private static readonly List<StaticTorch> ActiveStaticTorches = new();

        [SerializeField] private Transform flamePoint;
        [SerializeField] private TorchFlamePresenter flamePresenter;
        [SerializeField] private Collider igniteCollider;
        [SerializeField] private float igniteDuration = 1.5f;
        [SerializeField] private float igniteSpeedMultiplier = 1.5f;
        [SerializeField] private bool startLit;

        [Header("Quest")]
        [SerializeField] private QuestSO questOnIgnite;
        [SerializeField] private int questProgressAmount = 1;

        [Header("Ground snap")]
        [SerializeField] private bool snapToGroundOnStart;
        [SerializeField] private Transform baseSnapPivot;
        [SerializeField] private float groundSnapOffset;
        [SerializeField] private LayerMask groundMask = ~0;

        private float _igniteProgress;
        private bool _questProgressReported;

        public bool IsLit => flamePresenter != null && flamePresenter.IsLit;
        public Transform FlamePoint => flamePoint != null ? flamePoint : transform;
        public float IgniteSpeedMultiplier => Mathf.Max(igniteSpeedMultiplier, 0.01f);

        public static IReadOnlyList<StaticTorch> ActiveInScene => ActiveStaticTorches;

        private void Reset()
        {
            flamePoint = transform;
            flamePresenter = GetComponentInChildren<TorchFlamePresenter>(true);
            ResolveIgniteCollider();
        }

        private void Start()
        {
            if (snapToGroundOnStart)
            {
                SnapToGroundBelow();
            }
        }

        private void Awake()
        {
            if (flamePresenter == null)
            {
                flamePresenter = GetComponentInChildren<TorchFlamePresenter>(true);
            }

            ResolveIgniteCollider();

            if (GetComponent<ObjectOutlineHighlight>() == null)
            {
                gameObject.AddComponent<ObjectOutlineHighlight>();
            }

            if (startLit)
            {
                flamePresenter?.SetLit(true);
            }
            else
            {
                flamePresenter?.SetLit(false);
            }
        }

        public bool ContainsPoint(Vector3 worldPoint)
        {
            ResolveIgniteCollider();
            return TorchIgnitionColliderUtility.ContainsWorldPoint(igniteCollider, worldPoint);
        }

        public static StaticTorch FindBestLitForTip(Vector3 tipWorld)
        {
            StaticTorch best = null;
            var bestSqr = float.MaxValue;

            for (var i = 0; i < ActiveStaticTorches.Count; i++)
            {
                var torch = ActiveStaticTorches[i];
                if (torch == null || !torch.IsLit || !torch.ContainsPoint(tipWorld))
                {
                    continue;
                }

                var sqr = (torch.FlamePoint.position - tipWorld).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = torch;
                }
            }

            return best;
        }

        public void TryAccumulateIgnite(Vector3 tipWorld, bool carrierLit, bool carrierAimedDown, float deltaTime)
        {
            if (IsLit || !carrierLit || !carrierAimedDown)
            {
                _igniteProgress = 0f;
                return;
            }

            if (!ContainsPoint(tipWorld))
            {
                _igniteProgress = 0f;
                return;
            }

            _igniteProgress += deltaTime;
            if (_igniteProgress >= igniteDuration)
            {
                _igniteProgress = 0f;
                flamePresenter?.SetLit(true);
                ReportIgniteQuestProgress();
            }
        }

        private void ReportIgniteQuestProgress()
        {
            if (_questProgressReported || questOnIgnite == null || questProgressAmount == 0)
            {
                return;
            }

            _questProgressReported = true;
            QuestBus.GetInstance().OnUpdateCounter?.Invoke(
                questOnIgnite.QuestId,
                questProgressAmount);
        }

        private void OnEnable()
        {
            if (!ActiveStaticTorches.Contains(this))
            {
                ActiveStaticTorches.Add(this);
            }
        }

        private void OnDisable()
        {
            ActiveStaticTorches.Remove(this);
        }

        private void OnValidate()
        {
            ResolveIgniteCollider();
        }

        private void ResolveIgniteCollider()
        {
            if (igniteCollider != null)
            {
                return;
            }

            igniteCollider = GetComponent<Collider>();
            if (igniteCollider == null)
            {
                igniteCollider = GetComponentInChildren<Collider>(true);
            }
        }

        public void SnapToGroundBelow()
        {
            if (!TryGetBaseLocalOffset(out var baseLocalOffset))
            {
                return;
            }

            if (!DecorationPlacementUtility.TrySampleTopGroundAt(
                    transform.position, groundMask, transform, out var hit))
            {
                return;
            }

            var normal = hit.normal.sqrMagnitude > 0.0001f ? hit.normal.normalized : Vector3.up;
            var currentBaseWorld = transform.TransformPoint(baseLocalOffset);
            var targetBaseWorld = hit.point + normal * groundSnapOffset;
            transform.position += targetBaseWorld - currentBaseWorld;
        }

        private bool TryGetBaseLocalOffset(out Vector3 baseLocalOffset)
        {
            if (baseSnapPivot != null)
            {
                baseLocalOffset = transform.InverseTransformPoint(baseSnapPivot.position);
                return true;
            }

            if (TryGetRendererBottomLocalOffset(out baseLocalOffset))
            {
                return true;
            }

            baseLocalOffset = Vector3.zero;
            return true;
        }

        private bool TryGetRendererBottomLocalOffset(out Vector3 baseLocalOffset)
        {
            baseLocalOffset = Vector3.zero;
            var minY = float.MaxValue;
            var found = false;
            var renderers = GetComponentsInChildren<Renderer>(false);

            for (var i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] is ParticleSystemRenderer)
                {
                    continue;
                }

                minY = Mathf.Min(minY, renderers[i].bounds.min.y);
                found = true;
            }

            if (!found)
            {
                return false;
            }

            baseLocalOffset = transform.InverseTransformPoint(
                new Vector3(transform.position.x, minY, transform.position.z));
            return true;
        }

        private void OnDrawGizmosSelected()
        {
            ResolveIgniteCollider();
            Gizmos.color = new Color(1f, 0.7f, 0.2f, 0.35f);

            if (igniteCollider is SphereCollider sphere)
            {
                Gizmos.matrix = igniteCollider.transform.localToWorldMatrix;
                Gizmos.DrawWireSphere(sphere.center, sphere.radius);
                return;
            }

            if (igniteCollider != null)
            {
                Gizmos.matrix = Matrix4x4.identity;
                Gizmos.DrawWireCube(igniteCollider.bounds.center, igniteCollider.bounds.size);
            }
        }
    }
}
