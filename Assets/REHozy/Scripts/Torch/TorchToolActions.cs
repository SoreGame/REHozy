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

        [Header("Fuel")]
        [SerializeField] private float igniteFromSourceDuration = 0.9f;
        [SerializeField] private float idleBurnDuration = 12f;
        [SerializeField] private float burnDuration = 25f;
        [SerializeField] private float idleSpeedThreshold = 0.2f;
        [Header("Movement burn-out")]
        [SerializeField] private float movementBurnReferenceSpeed = 2.5f;
        [SerializeField] private float movementBurnExtraPerReferenceSpeed = 1.5f;
        [SerializeField] private float maxMovementBurnMultiplier = 5f;

        private CarryableToolCore _core;
        private float _igniteProgress01;
        private float _fuel01;
        private bool _isLit;
        private bool _returnHoldInProgress;
        private Vector3 _lastRootPosition;
        private bool _hasLastRootPosition;

        public bool IsLit => _isLit;
        public float BarFill01 => _isLit ? _fuel01 : _igniteProgress01;
        public bool IsIgniting => !_isLit && (_igniteProgress01 > 0f || IsNearFireSource());
        public bool IsRefueling => _isLit && _fuel01 < 1f && IsNearFireSource();
        public bool ShouldShowFuelBar =>
            _core != null
            && _core.State == CarryableToolState.Carried
            && aimPivot != null
            && !_returnHoldInProgress
            && aimPivot.AimTilt01 > 0.05f
            && (_isLit
                ? _fuel01 > 0f || IsRefueling
                : _igniteProgress01 > 0f || (aimPivot.IsAimedDownEnough && IsNearFireSource()));

        public Vector3 BarWorldAnchor
        {
            get
            {
                if (flamePresenter != null)
                {
                    return flamePresenter.transform.position;
                }

                return aimPivot != null ? aimPivot.Tip.position : transform.position;
            }
        }

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

            ExtinguishAndReset();
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
                _hasLastRootPosition = false;
                if (_isLit || _fuel01 > 0f || _igniteProgress01 > 0f)
                {
                    ExtinguishAndReset();
                }

                return;
            }

            var horizontalSpeed = SampleHorizontalSpeed();

            if (_returnHoldInProgress || aimPivot == null)
            {
                ClampVisualTipAboveWater();
                return;
            }

            if (!aimPivot.IsAimedDownEnough)
            {
                if (_isLit || _fuel01 > 0.001f || _igniteProgress01 > 0.001f)
                {
                    ExtinguishAndReset();
                }
            }
            else
            {
                var tip = aimPivot.Tip.position;

                if (!_isLit)
                {
                    TryIgniteFromNearbyFire(tip, Time.deltaTime);
                }
                else
                {
                    BurnFuel(Time.deltaTime, horizontalSpeed);
                    TryRefuelFromNearbyFire(tip, Time.deltaTime);
                    TryIgniteStaticTorches(tip, Time.deltaTime);
                }
            }

            ClampVisualTipAboveWater();
        }

        private void ClampVisualTipAboveWater()
        {
            var carry = _core.CarryDriver;
            if (carry == null || !carry.ClampTipAboveWater || aimPivot == null)
            {
                return;
            }

            var root = _core.transform;
            root.position = WaterCarryClamp.ClampRootSoTipAboveWater(
                root,
                aimPivot.Tip,
                root.position,
                root.rotation,
                carry.WaterTipClearance,
                carry.GroundMask);
        }

        private void TryIgniteFromNearbyFire(Vector3 tip, float deltaTime)
        {
            if (!TryGetBestNearbyFireSource(tip, out var speedMult))
            {
                _igniteProgress01 = 0f;
                return;
            }

            var rate = speedMult / Mathf.Max(igniteFromSourceDuration, 0.01f);
            _igniteProgress01 = Mathf.Clamp01(_igniteProgress01 + deltaTime * rate);
            if (_igniteProgress01 < 1f)
            {
                return;
            }

            _igniteProgress01 = 0f;
            _fuel01 = 1f;
            SetLit(true);
        }

        private void TryRefuelFromNearbyFire(Vector3 tip, float deltaTime)
        {
            if (_fuel01 >= 1f || !TryGetBestNearbyFireSource(tip, out var speedMult))
            {
                return;
            }

            var rate = speedMult / Mathf.Max(igniteFromSourceDuration, 0.01f);
            _fuel01 = Mathf.Clamp01(_fuel01 + deltaTime * rate);
        }

        private static bool TryGetBestNearbyFireSource(Vector3 tip, out float speedMultiplier)
        {
            speedMultiplier = 1f;
            var bestSqr = float.MaxValue;
            var found = false;

            var source = TorchIgnitionSource.FindBestForTip(tip);
            if (source != null)
            {
                found = true;
                bestSqr = (source.IgnitePoint.position - tip).sqrMagnitude;
                speedMultiplier = source.IgniteSpeedMultiplier;
            }

            var litStatic = StaticTorch.FindBestLitForTip(tip);
            if (litStatic != null)
            {
                var sqr = (litStatic.FlamePoint.position - tip).sqrMagnitude;
                if (!found || sqr < bestSqr)
                {
                    speedMultiplier = litStatic.IgniteSpeedMultiplier;
                    found = true;
                }
            }

            return found;
        }

        private void BurnFuel(float deltaTime, float horizontalSpeed)
        {
            SetLit(true);
            var isIdle = horizontalSpeed <= idleSpeedThreshold;
            var duration = isIdle ? idleBurnDuration : burnDuration;
            var burnRate = 1f / Mathf.Max(duration, 0.01f);
            if (!isIdle)
            {
                burnRate *= GetMovementBurnMultiplier(horizontalSpeed);
            }

            _fuel01 -= deltaTime * burnRate;
            if (_fuel01 <= 0f)
            {
                ExtinguishAndReset();
            }
        }

        private float SampleHorizontalSpeed()
        {
            var rootPos = _core.transform.position;
            var deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
            var speed = 0f;

            if (_hasLastRootPosition)
            {
                var delta = rootPos - _lastRootPosition;
                speed = new Vector3(delta.x, 0f, delta.z).magnitude / deltaTime;
            }

            _lastRootPosition = rootPos;
            _hasLastRootPosition = true;
            return speed;
        }

        private float GetMovementBurnMultiplier(float horizontalSpeed)
        {
            var reference = Mathf.Max(movementBurnReferenceSpeed, 0.01f);
            var extra = movementBurnExtraPerReferenceSpeed * (horizontalSpeed / reference);
            return Mathf.Clamp(1f + extra, 1f, Mathf.Max(maxMovementBurnMultiplier, 1f));
        }

        private void TryIgniteStaticTorches(Vector3 tip, float deltaTime)
        {
            var aimedDown = aimPivot.IsAimedDownEnough;
            var staticTorches = StaticTorch.ActiveInScene;
            for (var i = 0; i < staticTorches.Count; i++)
            {
                var staticTorch = staticTorches[i];
                if (staticTorch == null || staticTorch.IsLit)
                {
                    continue;
                }

                staticTorch.TryAccumulateIgnite(tip, _isLit, aimedDown, deltaTime);
            }
        }

        private void SetLit(bool lit)
        {
            _isLit = lit;
            flamePresenter?.SetLit(lit);
        }

        private void ExtinguishAndReset()
        {
            _fuel01 = 0f;
            _igniteProgress01 = 0f;
            SetLit(false);
        }

        private bool IsNearFireSource()
        {
            if (aimPivot == null)
            {
                return false;
            }

            return TryGetBestNearbyFireSource(aimPivot.Tip.position, out _);
        }
    }
}
