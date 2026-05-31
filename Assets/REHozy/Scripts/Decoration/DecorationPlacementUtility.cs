using System;
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
            if (helper == null || helper.WaterVolume == null)
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
            LayerMask groundMask,
            out Vector3 anchor,
            out Vector3 surfaceNormal)
        {
            anchor = default;
            surfaceNormal = Vector3.up;

            if (camera == null || !CarryableMouseRay.TryGetRay(camera, out var ray))
            {
                return false;
            }

            var hits = Physics.RaycastAll(ray, 200f, groundMask, QueryTriggerInteraction.Ignore);
            if (hits.Length == 0)
            {
                return false;
            }

            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var hit in hits)
            {
                if (hit.collider == null)
                {
                    continue;
                }

                anchor = hit.point;
                surfaceNormal = hit.normal;
                return true;
            }

            return false;
        }

        private static float SampleDownwardGroundY(Vector3 worldPoint, LayerMask groundMask)
        {
            return TrySampleTopGroundAt(worldPoint, groundMask, null, out var hit)
                ? hit.point.y
                : float.NaN;
        }

        public static bool TrySampleTopGroundAt(
            Vector3 worldPoint,
            LayerMask groundMask,
            Transform ignoreRoot,
            out RaycastHit bestHit,
            float rayStartHeight = 50f)
        {
            bestHit = default;
            var origin = new Vector3(worldPoint.x, worldPoint.y + rayStartHeight, worldPoint.z);
            var hits = Physics.RaycastAll(
                origin,
                Vector3.down,
                rayStartHeight + 100f,
                groundMask,
                QueryTriggerInteraction.Ignore);

            if (hits.Length == 0)
            {
                return false;
            }

            var found = false;
            var bestY = float.MinValue;

            foreach (var hit in hits)
            {
                if (hit.collider == null)
                {
                    continue;
                }

                if (ignoreRoot != null
                    && (hit.collider.transform == ignoreRoot || hit.collider.transform.IsChildOf(ignoreRoot)))
                {
                    continue;
                }

                if (hit.point.y > bestY)
                {
                    bestY = hit.point.y;
                    bestHit = hit;
                    found = true;
                }
            }

            if (found)
            {
                RefineTerrainSnapHit(ref bestHit);
            }

            return found;
        }

        public static void RefineTerrainSnapHit(ref RaycastHit hit)
        {
            if (hit.collider is not TerrainCollider terrainCollider)
            {
                return;
            }

            var terrain = terrainCollider.GetComponent<Terrain>();
            if (terrain == null)
            {
                return;
            }

            var terrainY = terrain.SampleHeight(hit.point) + terrain.transform.position.y;
            hit.point = new Vector3(hit.point.x, terrainY, hit.point.z);
            hit.normal = terrain.terrainData.GetInterpolatedNormal(
                (hit.point.x - terrain.transform.position.x) / terrain.terrainData.size.x,
                (hit.point.z - terrain.transform.position.z) / terrain.terrainData.size.z);
        }

        public static void ComputeRootPositionAtAnchor(
            Transform root,
            Transform placementPivot,
            Vector3 groundAnchor,
            Vector3 surfaceNormal,
            float groundSnapOffset,
            Quaternion targetRootRotation,
            out Vector3 rootPosition)
        {
            var pivot = placementPivot != null ? placementPivot : root;
            var normal = surfaceNormal.sqrMagnitude > 0.0001f ? surfaceNormal.normalized : Vector3.up;
            var pivotLocalOffset = root.InverseTransformPoint(pivot.position);
            var targetPivot = groundAnchor + normal * groundSnapOffset;
            rootPosition = targetPivot - targetRootRotation * pivotLocalOffset;
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
