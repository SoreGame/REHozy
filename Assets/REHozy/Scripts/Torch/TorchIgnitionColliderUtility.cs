using UnityEngine;

namespace REHozy.Torch
{
    public static class TorchIgnitionColliderUtility
    {
        private const float InsideEpsilonSqr = 1e-6f;
        private const float OverlapProbeRadius = 0.02f;

        public static bool ContainsWorldPoint(Collider collider, Vector3 worldPoint)
        {
            if (collider == null || !collider.enabled)
            {
                return false;
            }

            switch (collider)
            {
                case SphereCollider sphere:
                    return ContainsWorldPoint(sphere, worldPoint);
                case BoxCollider box:
                    return ContainsWorldPoint(box, worldPoint);
                case CapsuleCollider capsule:
                    return ContainsWorldPoint(capsule, worldPoint);
                default:
                    return ContainsWorldPointViaOverlap(collider, worldPoint);
            }
        }

        public static bool ContainsWorldPoint(SphereCollider sphere, Vector3 worldPoint)
        {
            if (sphere == null || !sphere.enabled)
            {
                return false;
            }

            var local = sphere.transform.InverseTransformPoint(worldPoint) - sphere.center;
            return local.sqrMagnitude <= sphere.radius * sphere.radius + InsideEpsilonSqr;
        }

        public static bool ContainsWorldPoint(BoxCollider box, Vector3 worldPoint)
        {
            if (box == null || !box.enabled)
            {
                return false;
            }

            var local = box.transform.InverseTransformPoint(worldPoint) - box.center;
            var half = box.size * 0.5f;
            return Mathf.Abs(local.x) <= half.x
                && Mathf.Abs(local.y) <= half.y
                && Mathf.Abs(local.z) <= half.z;
        }

        public static bool ContainsWorldPoint(CapsuleCollider capsule, Vector3 worldPoint)
        {
            if (capsule == null || !capsule.enabled)
            {
                return false;
            }

            var local = capsule.transform.InverseTransformPoint(worldPoint) - capsule.center;
            var radius = capsule.radius;
            var height = Mathf.Max(capsule.height * 0.5f - radius, 0f);

            float axial;
            float radial;
            switch (capsule.direction)
            {
                case 0:
                    axial = local.x;
                    radial = new Vector2(local.y, local.z).magnitude;
                    break;
                case 1:
                    axial = local.y;
                    radial = new Vector2(local.x, local.z).magnitude;
                    break;
                default:
                    axial = local.z;
                    radial = new Vector2(local.x, local.y).magnitude;
                    break;
            }

            var clampedAxial = Mathf.Clamp(axial, -height, height);
            var axisPoint = Vector3.zero;
            switch (capsule.direction)
            {
                case 0:
                    axisPoint = new Vector3(clampedAxial, 0f, 0f);
                    break;
                case 1:
                    axisPoint = new Vector3(0f, clampedAxial, 0f);
                    break;
                default:
                    axisPoint = new Vector3(0f, 0f, clampedAxial);
                    break;
            }

            return (local - axisPoint).sqrMagnitude <= radius * radius + InsideEpsilonSqr;
        }

        private static bool ContainsWorldPointViaOverlap(Collider collider, Vector3 worldPoint)
        {
            var hits = Physics.OverlapSphere(
                worldPoint,
                OverlapProbeRadius,
                ~0,
                QueryTriggerInteraction.Collide);

            for (var i = 0; i < hits.Length; i++)
            {
                if (hits[i] == collider)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
