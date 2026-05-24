using Bitgem.VFX.StylisedWater;
using REHozy.CarryableTools;
using UnityEngine;

namespace REHozy.Decoration
{
    public static class DecorationPlacementUtility
    {
        private const float WaterSurfaceClearance = 0.08f;

        public static bool TryGetWaterSurfaceY(Vector3 worldPoint, out float waterY)
        {
            waterY = 0f;
            var helper = WaterVolumeHelper.Instance;
            if (helper == null)
            {
                return false;
            }

            var height = helper.GetHeight(worldPoint);
            if (height == null)
            {
                return false;
            }

            waterY = height.Value;
            return true;
        }

        public static bool IsPlacementBlockedByWater(Vector3 anchorPoint)
        {
            if (!TryGetWaterSurfaceY(anchorPoint, out var waterY))
            {
                return false;
            }

            return anchorPoint.y < waterY + WaterSurfaceClearance;
        }

        public static bool IsValidPlacementAnchor(Vector3 anchorPoint)
        {
            return !IsPlacementBlockedByWater(anchorPoint);
        }

        public static bool TryResolvePlacementAnchor(
            UnityEngine.Camera camera,
            CarryableCarryDriver carryDriver,
            LayerMask groundMask,
            out Vector3 anchor,
            out Vector3 surfaceNormal)
        {
            anchor = default;
            surfaceNormal = Vector3.up;

            if (carryDriver != null && carryDriver.TryGetGroundAnchor(out anchor))
            {
                if (Physics.Raycast(anchor + Vector3.up * 0.05f, Vector3.down, out var hit, 5f, groundMask,
                        QueryTriggerInteraction.Ignore))
                {
                    surfaceNormal = hit.normal;
                }

                return true;
            }

            if (camera == null || !CarryableMouseRay.TryGetRay(camera, out var ray))
            {
                return false;
            }

            if (!Physics.Raycast(ray, out var surfaceHit, 200f, groundMask, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            anchor = surfaceHit.point;
            surfaceNormal = surfaceHit.normal;

            var downOrigin = new Vector3(anchor.x, anchor.y + 50f, anchor.z);
            if (Physics.Raycast(downOrigin, Vector3.down, out var downHit, 100f, groundMask,
                    QueryTriggerInteraction.Ignore))
            {
                anchor = downHit.point;
                surfaceNormal = downHit.normal;
            }

            return true;
        }

        public static void ComputeRootPositionAtAnchor(
            Transform root,
            Transform placementPivot,
            Vector3 groundAnchor,
            Vector3 surfaceNormal,
            float groundSnapOffset,
            out Vector3 rootPosition)
        {
            var pivot = placementPivot != null ? placementPivot : root;
            var normal = surfaceNormal.sqrMagnitude > 0.0001f ? surfaceNormal.normalized : Vector3.up;
            var pivotWorldOffset = pivot.position - root.position;
            var targetPivot = groundAnchor + normal * groundSnapOffset;
            rootPosition = targetPivot - pivotWorldOffset;
        }

        public static Quaternion AlignRotationToSurface(Quaternion currentRotation, Vector3 surfaceNormal)
        {
            var normal = surfaceNormal.sqrMagnitude > 0.0001f ? surfaceNormal.normalized : Vector3.up;
            var referenceForward = Vector3.ProjectOnPlane(currentRotation * Vector3.forward, normal);
            if (referenceForward.sqrMagnitude < 0.0001f)
            {
                referenceForward = Vector3.ProjectOnPlane(Vector3.forward, normal);
            }

            if (referenceForward.sqrMagnitude < 0.0001f)
            {
                referenceForward = Vector3.Cross(normal, Vector3.right);
            }

            return Quaternion.LookRotation(referenceForward.normalized, normal);
        }
    }
}
