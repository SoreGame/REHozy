using REHozy.CarryableTools;
using UnityEngine;

namespace REHozy.Torch
{
    [DisallowMultipleComponent]
    [AddComponentMenu("REHozy/Torch/Torch Tool Actions")]
    [DefaultExecutionOrder(100)]
    public sealed class TorchToolActions : MonoBehaviour, ICarryableToolActions, ICarryableToolCarriedUpdate
    {
        [Header("References")]
        [SerializeField] private TorchAimPivot aimPivot;
        [SerializeField] private TorchFlamePresenter flamePresenter;

        [Header("Ignition")]
        [SerializeField] private float igniteFromSourceDuration = 1.5f;
        [SerializeField] private float staticTorchSearchRadius = 2f;
        [SerializeField] private LayerMask interactionMask = ~0;

        private static readonly Collider[] OverlapBuffer = new Collider[24];

        private CarryableToolCore _core;
        private float _sourceIgniteProgress;
        private bool _isLit;
        private bool _returnHoldInProgress;

        public bool IsLit => _isLit;

        private void Reset()
        {
            aimPivot = GetComponentInChildren<TorchAimPivot>(true);
            flamePresenter = GetComponentInChildren<TorchFlamePresenter>(true);
        }

        private void Awake()
        {
            _core = GetComponent<CarryableToolCore>();

            if (aimPivot == null)
            {
                aimPivot = GetComponentInChildren<TorchAimPivot>(true);
            }

            if (flamePresenter == null)
            {
                flamePresenter = GetComponentInChildren<TorchFlamePresenter>(true);
            }

            SetLit(false);
        }

        public bool HasCargo(CarryableToolCore tool) => false;

        public bool CanReturnHome(CarryableToolCore tool) => !_isLit;

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
            _returnHoldInProgress = returnHoldInProgress;

            if (tool.State != CarryableToolState.Carried || aimPivot == null)
            {
                return;
            }

            var wantLower = attackHeld && !returnHoldInProgress;
            aimPivot.UpdateAimTilt(wantLower, Time.deltaTime);
        }

        private void LateUpdate()
        {
            if (_core == null)
            {
                return;
            }

            if (_core.State != CarryableToolState.Carried)
            {
                aimPivot?.ResetAimTilt();
                ResetIgniteProgress();
                return;
            }

            if (_returnHoldInProgress || aimPivot == null || !aimPivot.IsAimedDownEnough)
            {
                ResetIgniteProgress();
                return;
            }

            var tip = aimPivot.Tip.position;

            if (!_isLit)
            {
                TryIgniteFromSource(tip, Time.deltaTime);
                return;
            }

            ResetSourceProgress();
            TryIgniteStaticTorches(tip, Time.deltaTime);
        }

        private void TryIgniteFromSource(Vector3 tip, float deltaTime)
        {
            var source = TorchIgnitionSource.FindBestForTip(tip);
            if (source == null)
            {
                _sourceIgniteProgress = 0f;
                return;
            }

            _sourceIgniteProgress += deltaTime;
            if (_sourceIgniteProgress >= igniteFromSourceDuration)
            {
                _sourceIgniteProgress = 0f;
                SetLit(true);
            }
        }

        private void TryIgniteStaticTorches(Vector3 tip, float deltaTime)
        {
            var count = Physics.OverlapSphereNonAlloc(
                tip,
                staticTorchSearchRadius,
                OverlapBuffer,
                interactionMask,
                QueryTriggerInteraction.Collide);

            for (var i = 0; i < count; i++)
            {
                var col = OverlapBuffer[i];
                if (col == null)
                {
                    continue;
                }

                var staticTorch = col.GetComponentInParent<StaticTorch>();
                if (staticTorch == null)
                {
                    continue;
                }

                staticTorch.TryAccumulateIgnite(tip, _isLit, aimPivot.IsAimedDownEnough, deltaTime);
            }
        }

        private void SetLit(bool lit)
        {
            _isLit = lit;
            flamePresenter?.SetLit(lit);
        }

        private void ResetIgniteProgress()
        {
            ResetSourceProgress();
        }

        private void ResetSourceProgress()
        {
            _sourceIgniteProgress = 0f;
        }
    }
}
