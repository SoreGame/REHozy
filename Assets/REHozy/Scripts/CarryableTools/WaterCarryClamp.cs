using REHozy.Decoration;
using UnityEngine;

namespace REHozy.CarryableTools
{
    public static class WaterCarryClamp
    {
        public const float DefaultTipClearance = 0.08f;

        public static Vector3 ClampRootSoTipAboveWater(
            Transform root,
            Transform tip,
            Vector3 rootPosition,
            Quaternion rootRotation,
            float clearance = DefaultTipClearance,
            LayerMask groundMask = default)
        {
            if (root == null || tip == null)
            {
                return rootPosition;
            }

            var tipLocal = tip.parent == root ? tip.localPosition : root.InverseTransformPoint(tip.position);
            var tipWorld = rootPosition + rootRotation * tipLocal;
            if (!TryGetMinTipHeight(tipWorld, groundMask, clearance, out var minTipY))
            {
                return rootPosition;
            }

            if (tipWorld.y >= minTipY)
            {
                return rootPosition;
            }

            return rootPosition + Vector3.up * (minTipY - tipWorld.y);
        }

        public static bool TryGetHighestGroundY(Vector3 worldPoint, LayerMask groundMask, out float groundY)
        {
            groundY = default;
            if (groundMask.value == 0)
            {
                return false;
            }

            var origin = worldPoint + Vector3.up * 50f;
            var hits = Physics.RaycastAll(
                origin,
                Vector3.down,
                120f,
                groundMask,
                QueryTriggerInteraction.Ignore);

            var found = false;
            var bestY = float.MinValue;

            foreach (var hit in hits)
            {
                if (hit.collider == null || IsWaterLayer(hit.collider.gameObject.layer))
                {
                    continue;
                }

                if (hit.point.y > bestY)
                {
                    bestY = hit.point.y;
                    found = true;
                }
            }

            if (!found)
            {
                return false;
            }

            groundY = bestY;
            return true;
        }

        public static bool ShouldUseWaterSurfaceAt(
            Vector3 worldPoint,
            LayerMask groundMask,
            out Vector3 waterAnchor)
        {
            waterAnchor = default;
            if (!TryGetWaterSurfaceAnchor(worldPoint, out waterAnchor))
            {
                return false;
            }

            if (TryGetHighestGroundY(worldPoint, groundMask, out var groundY)
                && groundY > waterAnchor.y + 0.01f)
            {
                return false;
            }

            return true;
        }

        private static bool TryGetMinTipHeight(
            Vector3 tipWorld,
            LayerMask groundMask,
            float clearance,
            out float minTipY)
        {
            minTipY = float.NegativeInfinity;
            var hasFloor = false;

            if (DecorationPlacementUtility.TryGetWaterSurfaceY(tipWorld, out var waterY))
            {
                minTipY = waterY + clearance;
                hasFloor = true;
            }

            if (TryGetHighestGroundY(tipWorld, groundMask, out var groundY))
            {
                minTipY = hasFloor ? Mathf.Max(minTipY, groundY + clearance) : groundY + clearance;
                hasFloor = true;
            }

            return hasFloor;
        }

        public static bool IsWaterLayer(int layer)
        {
            var waterLayer = LayerMask.NameToLayer("Water");
            return waterLayer >= 0 && layer == waterLayer;
        }

        public static bool IsOverWaterAt(Vector3 worldPoint) =>
            DecorationPlacementUtility.TryGetWaterSurfaceY(worldPoint, out _);

        public static bool TryGetWaterSurfaceAnchor(Vector3 worldPoint, out Vector3 anchor)
        {
            anchor = default;
            if (!DecorationPlacementUtility.TryGetWaterSurfaceY(worldPoint, out var waterY))
            {
                return false;
            }

            anchor = new Vector3(worldPoint.x, waterY, worldPoint.z);
            return true;
        }

        public static bool TryGetGroundSurfaceAnchor(
            Vector3 worldPoint,
            LayerMask groundMask,
            out Vector3 anchor,
            out Vector3 surfaceNormal)
        {
            anchor = default;
            surfaceNormal = Vector3.up;

            if (!TryGetHighestGroundY(worldPoint, groundMask, out var groundY))
            {
                return false;
            }

            anchor = new Vector3(worldPoint.x, groundY, worldPoint.z);

            var origin = anchor + Vector3.up * 0.05f;
            if (Physics.Raycast(origin, Vector3.down, out var hit, 5f, groundMask, QueryTriggerInteraction.Ignore)
                && hit.collider != null
                && !IsWaterLayer(hit.collider.gameObject.layer))
            {
                surfaceNormal = hit.normal;
            }

            return true;
        }
    }
}
