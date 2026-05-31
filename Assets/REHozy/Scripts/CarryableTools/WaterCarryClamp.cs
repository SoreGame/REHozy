using REHozy.Decoration;
using UnityEngine;

namespace REHozy.CarryableTools
{
    public static class WaterCarryClamp
    {
        public const float DefaultTipClearance = 0.08f;
        public const float DefaultGroundProbeRadius = 0.45f;
        private const float GroundPenetrationSlack = 0.14f;

        private static readonly Collider[] GroundProbeBuffer = new Collider[32];

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
            return TryGetHighestGroundY(worldPoint, groundMask, DefaultGroundProbeRadius, out groundY);
        }

        public static bool TryGetHighestGroundY(
            Vector3 worldPoint,
            LayerMask groundMask,
            float probeRadius,
            out float groundY)
        {
            groundY = default;
            if (groundMask.value == 0)
            {
                return false;
            }

            var found = false;
            var bestY = float.MinValue;
            AccumulateRaycastGroundY(worldPoint, groundMask, ref bestY, ref found);

            var probeCenter = worldPoint + Vector3.up * 0.2f;
            var count = Physics.OverlapSphereNonAlloc(
                probeCenter,
                probeRadius,
                GroundProbeBuffer,
                groundMask,
                QueryTriggerInteraction.Ignore);

            for (var i = 0; i < count; i++)
            {
                var col = GroundProbeBuffer[i];
                if (col == null || IsWaterLayer(col.gameObject.layer))
                {
                    continue;
                }

                AccumulateColliderGroundY(col, worldPoint, ref bestY, ref found);
            }

            if (!found)
            {
                return false;
            }

            groundY = bestY;
            return true;
        }

        public static bool IsGroundBlockingFloat(
            Vector3 worldPoint,
            LayerMask groundMask,
            float waterY,
            float probeRadius = DefaultGroundProbeRadius)
        {
            if (groundMask.value == 0)
            {
                return false;
            }

            var probeCenter = worldPoint + Vector3.up * 0.15f;
            var count = Physics.OverlapSphereNonAlloc(
                probeCenter,
                probeRadius,
                GroundProbeBuffer,
                groundMask,
                QueryTriggerInteraction.Ignore);

            for (var i = 0; i < count; i++)
            {
                var col = GroundProbeBuffer[i];
                if (col == null || IsWaterLayer(col.gameObject.layer))
                {
                    continue;
                }

                if (!IsTouchingGroundCollider(col, worldPoint))
                {
                    continue;
                }

                if (col.bounds.max.y > waterY + 0.01f)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool TryResolveGroundPenetration(
            ref Vector3 worldPoint,
            LayerMask groundMask,
            float probeRadius,
            float waterY)
        {
            if (groundMask.value == 0)
            {
                return false;
            }

            var resolved = false;
            for (var attempt = 0; attempt < 4; attempt++)
            {
                var pushed = false;
                var probeCenter = worldPoint + Vector3.up * 0.15f;
                var count = Physics.OverlapSphereNonAlloc(
                    probeCenter,
                    probeRadius,
                    GroundProbeBuffer,
                    groundMask,
                    QueryTriggerInteraction.Ignore);

                for (var i = 0; i < count; i++)
                {
                    var col = GroundProbeBuffer[i];
                    if (col == null || IsWaterLayer(col.gameObject.layer))
                    {
                        continue;
                    }

                    if (!IsTouchingGroundCollider(col, worldPoint))
                    {
                        continue;
                    }

                    if (col.bounds.max.y <= waterY + 0.01f)
                    {
                        continue;
                    }

                    var closest = col.ClosestPoint(worldPoint);
                    var away = worldPoint - closest;
                    var dist = away.magnitude;
                    Vector3 push;
                    if (dist > 0.0001f)
                    {
                        push = away / dist * (GroundPenetrationSlack - dist);
                    }
                    else
                    {
                        var fromCenter = worldPoint - col.bounds.center;
                        fromCenter.y = 0f;
                        push = fromCenter.sqrMagnitude > 0.0001f
                            ? fromCenter.normalized * GroundPenetrationSlack
                            : Vector3.right * GroundPenetrationSlack;
                    }

                    worldPoint += new Vector3(push.x, Mathf.Max(0f, push.y), push.z);
                    pushed = true;
                    resolved = true;
                }

                if (!pushed)
                {
                    break;
                }
            }

            return resolved;
        }

        private static void AccumulateRaycastGroundY(
            Vector3 worldPoint,
            LayerMask groundMask,
            ref float bestY,
            ref bool found)
        {
            var origin = worldPoint + Vector3.up * 50f;
            var hits = Physics.RaycastAll(
                origin,
                Vector3.down,
                120f,
                groundMask,
                QueryTriggerInteraction.Ignore);

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
        }

        private static void AccumulateColliderGroundY(
            Collider col,
            Vector3 worldPoint,
            ref float bestY,
            ref bool found)
        {
            var closest = col.ClosestPoint(worldPoint);
            if (closest.y > bestY)
            {
                bestY = closest.y;
                found = true;
            }

            if (!IsTouchingGroundCollider(col, worldPoint))
            {
                return;
            }

            if (col.bounds.max.y > bestY)
            {
                bestY = col.bounds.max.y;
                found = true;
            }
        }

        private static bool IsTouchingGroundCollider(Collider col, Vector3 worldPoint)
        {
            var closest = col.ClosestPoint(worldPoint);
            return (worldPoint - closest).sqrMagnitude <= GroundPenetrationSlack * GroundPenetrationSlack;
        }

        public static bool ShouldUseWaterSurfaceAt(
            Vector3 worldPoint,
            LayerMask groundMask,
            out Vector3 waterAnchor)
        {
            return ShouldUseWaterSurfaceAt(worldPoint, groundMask, DefaultGroundProbeRadius, out waterAnchor);
        }

        public static bool ShouldUseWaterSurfaceAt(
            Vector3 worldPoint,
            LayerMask groundMask,
            float probeRadius,
            out Vector3 waterAnchor)
        {
            waterAnchor = default;
            if (!TryGetWaterSurfaceAnchor(worldPoint, out waterAnchor))
            {
                return false;
            }

            if (DecorationPlacementUtility.TrySampleTopGroundAt(
                    worldPoint, groundMask, null, out var groundHit)
                && groundHit.point.y > waterAnchor.y + 0.01f)
            {
                return false;
            }

            if (IsGroundBlockingFloat(worldPoint, groundMask, waterAnchor.y, probeRadius))
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

            if (!ShouldUseWaterSurfaceAt(tipWorld, groundMask, out var waterAnchor))
            {
                return false;
            }

            minTipY = waterAnchor.y + clearance;
            return true;
        }

        public static bool IsWaterLayer(int layer)
        {
            var waterLayer = LayerMask.NameToLayer("Water");
            return waterLayer >= 0 && layer == waterLayer;
        }

        public static bool IsOverWaterAt(Vector3 worldPoint, LayerMask groundMask) =>
            ShouldUseWaterSurfaceAt(worldPoint, groundMask, out _);

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
