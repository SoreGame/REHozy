using UnityEngine;

namespace REHozy.CarryableTools
{
    /// <summary>
    /// Hard (non-penetrating) collision for kinematic floaters against Ground-layer colliders.
    /// Uses overlap queries + <see cref="Physics.ComputePenetration"/> — not soft forces.
    /// </summary>
    public static class FloatingGroundCollision
    {
        public const float DefaultSkinWidth = 0.02f;
        private const int MaxDepenetrateIterations = 16;
        private const int MoveBinarySearchIterations = 8;

        private static readonly Collider[] OverlapBuffer = new Collider[48];

        public readonly struct BodyShape
        {
            public readonly Collider Collider;
            public readonly Vector3 LocalPosition;
            public readonly Quaternion LocalRotation;
            public readonly float ProbeRadius;
            public readonly float SkinWidth;

            public BodyShape(Collider collider, Transform root, float skinWidth = DefaultSkinWidth)
            {
                Collider = collider;
                SkinWidth = skinWidth;

                if (collider != null && root != null)
                {
                    LocalPosition = root.InverseTransformPoint(collider.transform.position);
                    LocalRotation = Quaternion.Inverse(root.rotation) * collider.transform.rotation;
                    var extents = collider.bounds.extents;
                    ProbeRadius = Mathf.Max(extents.x, extents.y, extents.z) + skinWidth;
                }
                else
                {
                    LocalPosition = Vector3.zero;
                    LocalRotation = Quaternion.identity;
                    ProbeRadius = WaterCarryClamp.DefaultGroundProbeRadius;
                }
            }

            public bool IsValid => Collider != null && Collider.enabled;

            public void GetWorldPose(Vector3 rootPosition, Quaternion rootRotation, out Vector3 position, out Quaternion rotation)
            {
                position = rootPosition + rootRotation * LocalPosition;
                rotation = rootRotation * LocalRotation;
            }
        }

        public static bool TryCreateBodyShape(Transform root, float skinWidth, out BodyShape shape)
        {
            var col = root.GetComponentInChildren<Collider>();
            if (col == null || !col.enabled)
            {
                shape = default;
                return false;
            }

            shape = new BodyShape(col, root, skinWidth);
            return true;
        }

        public static bool HasGroundOverlap(
            Vector3 rootPosition,
            Quaternion rootRotation,
            in BodyShape body,
            LayerMask groundMask)
        {
            if (!body.IsValid || groundMask.value == 0)
            {
                return false;
            }

            if (OverlapsGroundVolume(rootPosition, rootRotation, body, groundMask))
            {
                return true;
            }

            body.GetWorldPose(rootPosition, rootRotation, out var bodyPos, out var bodyRot);
            var count = QueryGroundColliders(bodyPos, body.ProbeRadius, groundMask);

            for (var i = 0; i < count; i++)
            {
                var ground = OverlapBuffer[i];
                if (ground == null || ground == body.Collider || IsWaterLayer(ground.gameObject.layer))
                {
                    continue;
                }

                if (Physics.ComputePenetration(
                        body.Collider,
                        bodyPos,
                        bodyRot,
                        ground,
                        ground.transform.position,
                        ground.transform.rotation,
                        out _,
                        out _))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool OverlapsGroundVolume(
            Vector3 rootPosition,
            Quaternion rootRotation,
            in BodyShape body,
            LayerMask groundMask)
        {
            body.GetWorldPose(rootPosition, rootRotation, out var bodyPos, out var bodyRot);
            var col = body.Collider;
            var skin = Vector3.one * body.SkinWidth;

            switch (col)
            {
                case BoxCollider box:
                {
                    var scale = col.transform.lossyScale;
                    var center = bodyPos + bodyRot * Vector3.Scale(box.center, scale);
                    var halfExtents = Vector3.Max(Vector3.zero, Vector3.Scale(box.size * 0.5f, scale) - skin);
                    return Physics.CheckBox(center, halfExtents, bodyRot, groundMask, QueryTriggerInteraction.Ignore);
                }
                case SphereCollider sphere:
                {
                    var scale = col.transform.lossyScale;
                    var maxScale = Mathf.Max(scale.x, scale.y, scale.z);
                    var center = bodyPos + bodyRot * Vector3.Scale(sphere.center, scale);
                    var radius = Mathf.Max(0.001f, sphere.radius * maxScale - body.SkinWidth);
                    return Physics.CheckSphere(center, radius, groundMask, QueryTriggerInteraction.Ignore);
                }
                case CapsuleCollider capsule:
                {
                    var scale = col.transform.lossyScale;
                    var center = bodyPos + bodyRot * Vector3.Scale(capsule.center, scale);
                    var radius = Mathf.Max(0.001f, body.ProbeRadius - body.SkinWidth);
                    return Physics.CheckSphere(center, radius, groundMask, QueryTriggerInteraction.Ignore);
                }
                default:
                    return Physics.CheckSphere(
                        bodyPos,
                        Mathf.Max(0.001f, body.ProbeRadius - body.SkinWidth),
                        groundMask,
                        QueryTriggerInteraction.Ignore);
            }
        }

        public static void HardDepenetrate(
            ref Vector3 rootPosition,
            Quaternion rootRotation,
            in BodyShape body,
            LayerMask groundMask)
        {
            if (!body.IsValid || groundMask.value == 0)
            {
                return;
            }

            for (var iteration = 0; iteration < MaxDepenetrateIterations; iteration++)
            {
                var moved = false;
                body.GetWorldPose(rootPosition, rootRotation, out var bodyPos, out var bodyRot);
                var count = QueryGroundColliders(bodyPos, body.ProbeRadius * 1.25f, groundMask);

                for (var i = 0; i < count; i++)
                {
                    var ground = OverlapBuffer[i];
                    if (ground == null || ground == body.Collider || IsWaterLayer(ground.gameObject.layer))
                    {
                        continue;
                    }

                    if (TryComputeSeparation(body, bodyPos, bodyRot, ground, out var direction, out var distance))
                    {
                        rootPosition += direction * (distance + body.SkinWidth);
                        moved = true;
                    }
                }

                if (!moved)
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Moves from <paramref name="from"/> toward <paramref name="to"/> without overlapping ground. Returns false if no motion allowed.
        /// </summary>
        public static bool TryAdvancePosition(
            Vector3 from,
            Vector3 to,
            Quaternion rootRotation,
            in BodyShape body,
            LayerMask groundMask,
            out Vector3 result)
        {
            result = from;

            if (!body.IsValid || groundMask.value == 0)
            {
                result = to;
                return true;
            }

            if (!HasGroundOverlap(to, rootRotation, body, groundMask))
            {
                result = to;
                return true;
            }

            var low = 0f;
            var high = 1f;

            for (var i = 0; i < MoveBinarySearchIterations; i++)
            {
                var mid = (low + high) * 0.5f;
                var candidate = Vector3.Lerp(from, to, mid);

                if (HasGroundOverlap(candidate, rootRotation, body, groundMask))
                {
                    high = mid;
                }
                else
                {
                    low = mid;
                }
            }

            result = Vector3.Lerp(from, to, low);
            HardDepenetrate(ref result, rootRotation, body, groundMask);

            return (result - from).sqrMagnitude > 0.000001f;
        }

        public static void SanitizePosition(
            ref Vector3 rootPosition,
            Quaternion rootRotation,
            in BodyShape body,
            LayerMask groundMask)
        {
            HardDepenetrate(ref rootPosition, rootRotation, body, groundMask);
        }

        private static int QueryGroundColliders(Vector3 center, float radius, LayerMask groundMask)
        {
            return Physics.OverlapSphereNonAlloc(
                center,
                radius,
                OverlapBuffer,
                groundMask,
                QueryTriggerInteraction.Ignore);
        }

        private static bool IsPenetratingCollider(
            in BodyShape body,
            Vector3 bodyPos,
            Quaternion bodyRot,
            Collider ground)
        {
            if (TryComputeSeparation(body, bodyPos, bodyRot, ground, out _, out _))
            {
                return true;
            }

            return IsWithinClosestPoint(body, bodyPos, ground);
        }

        private static bool TryComputeSeparation(
            in BodyShape body,
            Vector3 bodyPos,
            Quaternion bodyRot,
            Collider ground,
            out Vector3 direction,
            out float distance)
        {
            direction = default;
            distance = default;

            if (Physics.ComputePenetration(
                    body.Collider,
                    bodyPos,
                    bodyRot,
                    ground,
                    ground.transform.position,
                    ground.transform.rotation,
                    out direction,
                    out distance))
            {
                return true;
            }

            if (!IsWithinClosestPoint(body, bodyPos, ground))
            {
                return false;
            }

            var closest = ground.ClosestPoint(bodyPos);
            var away = bodyPos - closest;
            var dist = away.magnitude;
            if (dist > 0.0001f)
            {
                direction = away / dist;
                distance = body.SkinWidth;
                return true;
            }

            direction = Vector3.up;
            distance = body.SkinWidth;
            return true;
        }

        private static bool IsWithinClosestPoint(in BodyShape body, Vector3 bodyPos, Collider ground)
        {
            var closest = ground.ClosestPoint(bodyPos);
            var slack = body.SkinWidth + body.ProbeRadius * 0.15f;
            return (bodyPos - closest).sqrMagnitude <= slack * slack;
        }

        private static bool IsWaterLayer(int layer) => WaterCarryClamp.IsWaterLayer(layer);
    }
}
