using REHozy.CarryableTools;
using UnityEngine;

namespace REHozy.Dirt
{
    [DisallowMultipleComponent]
    [AddComponentMenu("REHozy/Dirt/Shovel Tool Actions")]
    public sealed class ShovelToolActions : MonoBehaviour, ICarryableToolActions, ICarryableToolCarriedUpdate,
        ICarryableAimOverride
    {
        [Header("Digging")]
        [SerializeField] private float digRadius = 0.45f;
        [SerializeField] private float digStrength = 1.25f;
        [SerializeField] private LayerMask dirtPatchMask = ~0;
        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField] private float workPlaneAcquireDistance = 120f;

        private static readonly Collider[] OverlapBuffer = new Collider[16];
        private static readonly RaycastHit[] RaycastHits = new RaycastHit[24];

        private bool _planeLockActive;
        private bool _hasWorkPlane;
        private Plane _workPlane;

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

        public bool TryOverrideAim(UnityEngine.Camera camera, out Vector3 anchor, out Vector3 planeNormal)
        {
            anchor = default;
            planeNormal = Vector3.up;

            if (!_planeLockActive || !_hasWorkPlane || camera == null)
            {
                return false;
            }

            if (!CarryableMouseRay.TryGetRay(camera, out var ray))
            {
                return false;
            }

            if (!_workPlane.Raycast(ray, out var distance))
            {
                return false;
            }

            anchor = ray.GetPoint(distance);
            planeNormal = _workPlane.normal;
            return true;
        }

        public void OnCarriedUpdate(CarryableToolCore tool, bool attackHeld, bool returnHoldInProgress)
        {
            if (tool.State != CarryableToolState.Carried)
            {
                ReleaseWorkPlane();
                return;
            }

            if (!attackHeld || returnHoldInProgress)
            {
                ReleaseWorkPlane();
                return;
            }

            if (!_planeLockActive)
            {
                TryAcquireWorkPlane(tool);
                _planeLockActive = true;
            }

            DigAtTip(tool);
        }

        private void ReleaseWorkPlane()
        {
            _planeLockActive = false;
            _hasWorkPlane = false;
        }

        private void TryAcquireWorkPlane(CarryableToolCore tool)
        {
            if (TryAcquirePlaneFromDirtUnderTip(tool))
            {
                return;
            }

            if (TryAcquirePlaneFromMouseRay(tool))
            {
                return;
            }

            var tip = tool.Tip.position;
            _workPlane = new Plane(Vector3.up, tip);
            _hasWorkPlane = true;
        }

        private bool TryAcquirePlaneFromDirtUnderTip(CarryableToolCore tool)
        {
            var tip = tool.Tip.position;
            var origin = tip + Vector3.up * 0.5f;
            if (!Physics.Raycast(origin, Vector3.down, out var hit, 3f, dirtPatchMask,
                    QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            var patch = hit.collider.GetComponentInParent<DirtDeformPatch>();
            if (patch == null)
            {
                return false;
            }

            patch.GetWorkPlane(out var pointOnPlane, out var normal);
            _workPlane = new Plane(normal, pointOnPlane);
            _hasWorkPlane = true;
            return true;
        }

        private bool TryAcquirePlaneFromMouseRay(CarryableToolCore tool)
        {
            var driver = tool.CarryDriver;
            var camera = driver != null ? driver.ResolveCameraForAim() : UnityEngine.Camera.main;

            if (camera == null || !CarryableMouseRay.TryGetRay(camera, out var ray))
            {
                return false;
            }

            var hitCount = Physics.RaycastNonAlloc(
                ray,
                RaycastHits,
                workPlaneAcquireDistance,
                dirtPatchMask | groundMask,
                QueryTriggerInteraction.Ignore);

            if (hitCount <= 0)
            {
                return false;
            }

            DirtDeformPatch closestPatch = null;
            var closestPatchDistance = float.MaxValue;
            RaycastHit? bestGround = null;

            for (var i = 0; i < hitCount; i++)
            {
                var hit = RaycastHits[i];
                if (hit.collider == null)
                {
                    continue;
                }

                var patch = hit.collider.GetComponentInParent<DirtDeformPatch>();
                if (patch != null && hit.distance < closestPatchDistance)
                {
                    closestPatchDistance = hit.distance;
                    closestPatch = patch;
                }

                if (Vector3.Dot(hit.normal, Vector3.up) < 0.55f)
                {
                    continue;
                }

                if (bestGround == null || hit.point.y < bestGround.Value.point.y)
                {
                    bestGround = hit;
                }
            }

            if (closestPatch != null)
            {
                closestPatch.GetWorkPlane(out var pointOnPlane, out var normal);
                _workPlane = new Plane(normal, pointOnPlane);
                _hasWorkPlane = true;
                return true;
            }

            if (!bestGround.HasValue)
            {
                return false;
            }

            var groundHit = bestGround.Value;
            _workPlane = new Plane(groundHit.normal, groundHit.point);
            _hasWorkPlane = true;
            return true;
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
