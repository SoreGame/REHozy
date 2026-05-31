using Bitgem.VFX.StylisedWater;
using REHozy.CarryableTools;
using UnityEngine;

namespace REHozy
{
    [DisallowMultipleComponent]
    [AddComponentMenu("REHozy/Water/Floating Target Wander")]
    public sealed class FloatingTargetWander : MonoBehaviour
    {
        [Header("Motion driver")]
        [SerializeField] private bool driveViaRigidbodyIfPresent = true;
        [SerializeField] private bool enforceKinematicRigidbody = true;

        [Header("Water")]
        [SerializeField] private WaterVolumeHelper waterVolumeHelper;
        [SerializeField] private bool applyWaterHeight = true;
        [SerializeField] private float waterHeightSnapSpeed = 16f;
        [SerializeField] private LayerMask groundMask = ~0;
        [Min(0f)]
        [SerializeField] private float groundClearance = 0.02f;
        [Min(0f)]
        [SerializeField] private float collisionSkinWidth = FloatingGroundCollision.DefaultSkinWidth;

        [Header("Wander")]
        [Min(0.01f)]
        [SerializeField] private float maxRadius = 6f;
        [Min(0f)]
        [SerializeField] private float maxSpeed = 1.2f;
        [Min(0f)]
        [SerializeField] private float acceleration = 1.8f;
        [Min(0f)]
        [SerializeField] private float drag = 0.9f;
        [Min(0.05f)]
        [SerializeField] private float directionChangeInterval = 1.4f;
        [Range(0f, 1f)]
        [SerializeField] private float directionSmoothing = 0.35f;

        [Header("Anchor pull (stay near start)")]
        [Min(0f)]
        [SerializeField] private float anchorPullStrength = 3.5f;
        [Min(0f)]
        [SerializeField] private float anchorPullExponent = 1.35f;

        [Header("Shore avoidance (stay on water)")]
        [Min(0f)]
        [SerializeField] private float shoreAvoidProbeDistance = 1.3f;
        [Min(0f)]
        [SerializeField] private float shoreAvoidStrength = 7f;
        [Min(0f)]
        [SerializeField] private float shoreAvoidBoostWhenOutside = 12f;

        [Header("Avoid ground colliders (rocks/shore objects)")]
        [Tooltip("If empty (0), falls back to GroundMask used for height clamping.")]
        [SerializeField] private LayerMask avoidGroundMask;
        [Min(0f)]
        [SerializeField] private float avoidGroundRadius = 1.1f;
        [Min(0f)]
        [SerializeField] private float avoidGroundStrength = 10f;

        [Header("Avoid other floaters")]
        [Min(0f)]
        [SerializeField] private float separationRadius = 0.9f;
        [Min(0f)]
        [SerializeField] private float separationStrength = 6f;

        [Header("Debug")]
        [SerializeField] private bool drawGizmos;

        private Vector3 _anchorWorld;
        private Vector2 _velocityXZ;
        private Vector2 _desiredDir;
        private float _nextDirChangeTime;
        private bool _hasValidWater;
        private Rigidbody _rb;
        private float _cachedBottomOffset;
        private float _cachedProbeRadius;
        private FloatingGroundCollision.BodyShape _bodyShape;
        private readonly Collider[] _overlapBuffer = new Collider[24];

        public void Configure(LayerMask ground)
        {
            groundMask = ground;
            var groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer >= 0)
            {
                groundMask |= 1 << groundLayer;
            }
        }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            if (_rb != null && enforceKinematicRigidbody)
            {
                _rb.isKinematic = true;
                _rb.useGravity = false;
            }

            _anchorWorld = transform.position;
            CacheColliderMetrics();
            PickNewDesiredDirection(immediate: true);
        }

        private void OnEnable()
        {
            // When re-enabled (e.g. after being dropped back into water), restart around current position.
            _hasValidWater = false;
            _anchorWorld = transform.position;
            _velocityXZ = Vector2.zero;
            CacheColliderMetrics();

            if (_rb == null)
            {
                _rb = GetComponent<Rigidbody>();
            }

            if (_rb != null && enforceKinematicRigidbody)
            {
                _rb.isKinematic = true;
                _rb.useGravity = false;
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
            }

            _nextDirChangeTime = Time.time + Random.Range(0f, directionChangeInterval);

            SanitizeCurrentPosition();
        }

        private void SanitizeCurrentPosition()
        {
            if (!_bodyShape.IsValid)
            {
                return;
            }

            var pos = transform.position;
            FloatingGroundCollision.SanitizePosition(ref pos, transform.rotation, _bodyShape, groundMask);
            ApplyPosition(pos);
        }

        private void ApplyPosition(Vector3 worldPos)
        {
            if (_rb != null && driveViaRigidbodyIfPresent)
            {
                _rb.MovePosition(worldPos);
            }
            else
            {
                transform.position = worldPos;
            }
        }

        private void Update()
        {
            var helper = waterVolumeHelper != null ? waterVolumeHelper : WaterVolumeHelper.Instance;
            if (helper == null)
            {
                return;
            }

            var dt = Time.deltaTime;
            if (dt <= 0f)
            {
                return;
            }

            var pos = transform.position;

            // Wait until the water volume is ready (WaterVolumeBase tiles get built during Update()).
            if (!_hasValidWater)
            {
                if (!IsSafeWaterPosition(pos))
                {
                    return;
                }

                _hasValidWater = true;
                _anchorWorld = pos;
            }

            if (Time.time >= _nextDirChangeTime)
            {
                PickNewDesiredDirection(immediate: false);
                _nextDirChangeTime = Time.time + directionChangeInterval;
            }

            var force = Vector2.zero;

            // 1) Base wander force.
            force += _desiredDir * acceleration;

            // 2) Pull back to anchor when outside radius.
            var anchorOffset = new Vector2(pos.x - _anchorWorld.x, pos.z - _anchorWorld.z);
            var dist = anchorOffset.magnitude;
            if (dist > maxRadius && dist > 0.0001f)
            {
                var excess01 = Mathf.Clamp01((dist - maxRadius) / Mathf.Max(0.0001f, maxRadius));
                var pull = Mathf.Pow(excess01, anchorPullExponent) * anchorPullStrength;
                force += (-anchorOffset / dist) * pull;
            }

            // 3) Shore avoidance. Treat "GetHeight == null" as "not water" (shore/outside volume).
            force += ComputeShoreAvoidanceForce(helper, pos);

            // 4) Avoid ground colliders (rocks/shore meshes) even if the point is still inside the water volume.
            force += ComputeGroundColliderAvoidanceForce(pos);

            // 5) Separate from other floaters so they don't clump or intersect.
            force += ComputeSeparationForce(pos);

            // Integrate velocity.
            if (drag > 0f)
            {
                // Exponential-like damping without needing FixedUpdate.
                var damp = Mathf.Clamp01(1f - drag * dt);
                _velocityXZ *= damp;
            }

            _velocityXZ += force * dt;
            _velocityXZ = Vector2.ClampMagnitude(_velocityXZ, maxSpeed);

            // Apply motion with water + hard ground collision.
            var newPos = pos;
            if (!TryApplyConstrainedStep(pos, dt, ref newPos))
            {
                DeflectVelocityAtShore(pos);
                TryApplyConstrainedStep(pos, dt, ref newPos);
            }

            newPos = ClampToAnchorRadius(newPos);

            if (applyWaterHeight)
            {
                newPos.y = ResolveFloatingHeight(newPos, dt);
            }

            EnforceHardGroundCollision(pos, ref newPos);

            ApplyPosition(newPos);
        }

        private void EnforceHardGroundCollision(Vector3 previousPos, ref Vector3 worldPos)
        {
            if (!_bodyShape.IsValid)
            {
                return;
            }

            FloatingGroundCollision.SanitizePosition(ref worldPos, transform.rotation, _bodyShape, groundMask);

            if (FloatingGroundCollision.HasGroundOverlap(worldPos, transform.rotation, _bodyShape, groundMask))
            {
                worldPos = previousPos;
                _velocityXZ *= 0.15f;
                FloatingGroundCollision.SanitizePosition(ref worldPos, transform.rotation, _bodyShape, groundMask);
            }
        }

        private float ResolveFloatingHeight(Vector3 worldPos, float dt)
        {
            var floorY = worldPos.y - _cachedBottomOffset;

            if (Decoration.DecorationPlacementUtility.TryGetWaterSurfaceY(worldPos, out var waterY))
            {
                floorY = Mathf.Max(floorY, waterY);
            }

            if (WaterCarryClamp.TryGetHighestGroundY(worldPos, groundMask, GetGroundProbeRadius(), out var groundY))
            {
                floorY = Mathf.Max(floorY, groundY + groundClearance);
            }

            var targetY = floorY + _cachedBottomOffset;
            return Mathf.Lerp(worldPos.y, targetY, 1f - Mathf.Exp(-waterHeightSnapSpeed * dt));
        }

        private void CacheColliderMetrics()
        {
            FloatingGroundCollision.TryCreateBodyShape(transform, collisionSkinWidth, out _bodyShape);

            var col = _bodyShape.Collider;
            if (col == null || !col.enabled)
            {
                _cachedBottomOffset = 0f;
                _cachedProbeRadius = Mathf.Max(avoidGroundRadius, WaterCarryClamp.DefaultGroundProbeRadius);
                return;
            }

            _cachedBottomOffset = transform.position.y - col.bounds.min.y;
            var extents = col.bounds.extents;
            _cachedProbeRadius = Mathf.Max(
                avoidGroundRadius,
                Mathf.Max(extents.x, extents.z) * 0.9f + collisionSkinWidth);
        }

        private float GetGroundProbeRadius() =>
            _cachedProbeRadius > 0f
                ? _cachedProbeRadius
                : Mathf.Max(avoidGroundRadius, WaterCarryClamp.DefaultGroundProbeRadius);

        private void PickNewDesiredDirection(bool immediate)
        {
            var rnd = PickSafeWanderDirection();
            if (rnd.sqrMagnitude < 0.0001f)
            {
                rnd = Vector2.right;
            }

            rnd.Normalize();

            if (immediate)
            {
                _desiredDir = rnd;
                return;
            }

            _desiredDir = Vector2.Lerp(_desiredDir, rnd, 1f - directionSmoothing);
            if (_desiredDir.sqrMagnitude < 0.0001f)
            {
                _desiredDir = rnd;
            }
            else
            {
                _desiredDir.Normalize();
            }

            if (_velocityXZ.sqrMagnitude > 0.0004f)
            {
                var speed = _velocityXZ.magnitude;
                _velocityXZ = Vector2.Lerp(_velocityXZ.normalized, _desiredDir, 0.25f).normalized * speed;
            }
        }

        private Vector2 PickSafeWanderDirection()
        {
            var pos = transform.position;
            var bestDir = Random.insideUnitCircle;
            var bestScore = -1f;

            for (var i = 0; i < 10; i++)
            {
                var candidate = Random.insideUnitCircle;
                if (candidate.sqrMagnitude < 0.0001f)
                {
                    continue;
                }

                candidate.Normalize();
                if (!IsDirectionSafeFrom(pos, candidate))
                {
                    continue;
                }

                var score = Vector2.Dot(candidate, _desiredDir);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestDir = candidate;
                }
            }

            if (bestScore >= 0f)
            {
                return bestDir;
            }

            var tangent = new Vector2(-_desiredDir.y, _desiredDir.x);
            if (Random.value < 0.5f)
            {
                tangent = -tangent;
            }

            return IsDirectionSafeFrom(pos, tangent) ? tangent : -_desiredDir;
        }

        private bool IsDirectionSafeFrom(Vector3 pos, Vector2 dir)
        {
            if (dir.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            dir.Normalize();
            var probeDist = Mathf.Max(0.35f, shoreAvoidProbeDistance * 0.45f);
            var probePos = new Vector3(pos.x + dir.x * probeDist, pos.y, pos.z + dir.y * probeDist);
            return IsSafeWaterPosition(probePos);
        }

        private void DeflectVelocityAtShore(Vector3 pos)
        {
            if (_velocityXZ.sqrMagnitude < 0.000001f)
            {
                return;
            }

            var speed = _velocityXZ.magnitude;
            var moveDir = _velocityXZ / speed;
            var left = new Vector2(-moveDir.y, moveDir.x);
            var stepDist = Mathf.Max(maxSpeed * Time.deltaTime, 0.05f);

            Vector2 bestDir = Vector2.zero;
            var bestScore = -2f;

            TrySlideDirection(pos, left, stepDist, ref bestDir, ref bestScore);
            TrySlideDirection(pos, -left, stepDist, ref bestDir, ref bestScore);

            var toAnchor = new Vector2(_anchorWorld.x - pos.x, _anchorWorld.z - pos.z);
            if (toAnchor.sqrMagnitude > 0.0001f)
            {
                TrySlideDirection(pos, toAnchor.normalized, stepDist, ref bestDir, ref bestScore);
            }

            if (bestScore > -0.25f)
            {
                _velocityXZ = bestDir * speed * 0.8f;
                return;
            }

            _velocityXZ *= 0.2f;
        }

        private void TrySlideDirection(
            Vector3 pos,
            Vector2 dir,
            float stepDist,
            ref Vector2 bestDir,
            ref float bestScore)
        {
            if (dir.sqrMagnitude < 0.0001f)
            {
                return;
            }

            dir.Normalize();
            var probe = new Vector3(pos.x + dir.x * stepDist, pos.y, pos.z + dir.y * stepDist);
            if (!IsSafeWaterPosition(probe))
            {
                return;
            }

            var score = Vector2.Dot(dir, _velocityXZ.normalized);
            if (score > bestScore)
            {
                bestScore = score;
                bestDir = dir;
            }
        }

        private Vector2 ComputeShoreAvoidanceForce(WaterVolumeHelper helper, Vector3 pos)
        {
            var currentIsWater = IsSafeWaterPosition(pos);

            // Choose a probe direction. Prefer current velocity, otherwise desired direction.
            var moveDir = _velocityXZ.sqrMagnitude > 0.001f ? _velocityXZ.normalized : _desiredDir;

            var forward = moveDir;
            var left = new Vector2(-moveDir.y, moveDir.x);
            var right = -left;

            var f = Vector2.zero;
            AccumulateProbe(ref f, pos, forward, shoreAvoidProbeDistance, shoreAvoidStrength);
            AccumulateProbe(ref f, pos, left, shoreAvoidProbeDistance, shoreAvoidStrength * 0.75f);
            AccumulateProbe(ref f, pos, right, shoreAvoidProbeDistance, shoreAvoidStrength * 0.75f);

            if (!currentIsWater)
            {
                // If we already got outside (e.g. scene edited or spawned too close), strongly push back.
                f += new Vector2(_anchorWorld.x - pos.x, _anchorWorld.z - pos.z).normalized * shoreAvoidBoostWhenOutside;
            }

            return f;
        }

        private Vector2 ComputeGroundColliderAvoidanceForce(Vector3 pos)
        {
            if (avoidGroundRadius <= 0f || avoidGroundStrength <= 0f)
            {
                return Vector2.zero;
            }

            var mask = avoidGroundMask.value != 0 ? avoidGroundMask : groundMask;
            if (mask.value == 0)
            {
                return Vector2.zero;
            }

            // Probe around current position; push away from the nearest points of ground colliders.
            var center = pos + Vector3.up * 0.15f;
            var count = Physics.OverlapSphereNonAlloc(
                center,
                avoidGroundRadius,
                _overlapBuffer,
                mask,
                QueryTriggerInteraction.Ignore);

            if (count <= 0)
            {
                return Vector2.zero;
            }

            var f = Vector2.zero;
            for (var i = 0; i < count; i++)
            {
                var col = _overlapBuffer[i];
                if (col == null || col.attachedRigidbody == _rb)
                {
                    continue;
                }

                if (REHozy.CarryableTools.WaterCarryClamp.IsWaterLayer(col.gameObject.layer))
                {
                    continue;
                }

                var closest = col.ClosestPoint(center);
                var away = new Vector2(center.x - closest.x, center.z - closest.z);
                var sqr = away.sqrMagnitude;
                if (sqr < 0.000001f)
                {
                    continue;
                }

                var dist = Mathf.Sqrt(sqr);
                var t = Mathf.Clamp01(1f - dist / Mathf.Max(0.0001f, avoidGroundRadius));
                away /= dist;
                f += away * (avoidGroundStrength * t);
            }

            return f;
        }

        private Vector2 ComputeSeparationForce(Vector3 pos)
        {
            if (separationRadius <= 0f || separationStrength <= 0f)
            {
                return Vector2.zero;
            }

            // We intentionally don't use a layer mask here; floaters can be on any layer.
            var center = pos + Vector3.up * 0.15f;
            var count = Physics.OverlapSphereNonAlloc(
                center,
                separationRadius,
                _overlapBuffer,
                ~0,
                QueryTriggerInteraction.Ignore);

            if (count <= 0)
            {
                return Vector2.zero;
            }

            var f = Vector2.zero;
            var considered = 0;

            for (var i = 0; i < count; i++)
            {
                var col = _overlapBuffer[i];
                if (col == null)
                {
                    continue;
                }

                var other = col.GetComponentInParent<FloatingTargetWander>();
                if (other == null || other == this || !other.isActiveAndEnabled)
                {
                    continue;
                }

                var otherPos = other.transform.position;
                var away = new Vector2(pos.x - otherPos.x, pos.z - otherPos.z);
                var sqr = away.sqrMagnitude;
                if (sqr < 0.000001f)
                {
                    // Degenerate overlap: random small nudge.
                    away = Random.insideUnitCircle.normalized * 0.001f;
                    sqr = away.sqrMagnitude;
                }

                var dist = Mathf.Sqrt(sqr);
                var t = Mathf.Clamp01(1f - dist / Mathf.Max(0.0001f, separationRadius));
                away /= dist;
                f += away * (separationStrength * t);
                considered++;

                if (considered >= 6)
                {
                    // Avoid huge forces in dense packs; also keeps CPU stable.
                    break;
                }
            }

            return f;
        }

        private void AccumulateProbe(
            ref Vector2 force,
            Vector3 pos,
            Vector2 dir,
            float dist,
            float strength)
        {
            if (dir.sqrMagnitude < 0.0001f || dist <= 0f || strength <= 0f)
            {
                return;
            }

            dir.Normalize();
            var probePos = new Vector3(pos.x + dir.x * dist, pos.y, pos.z + dir.y * dist);
            if (!IsSafeWaterPosition(probePos))
            {
                force += -dir * strength;
            }
        }

        private bool TryApplyConstrainedStep(Vector3 from, float dt, ref Vector3 to)
        {
            if (_velocityXZ.sqrMagnitude < 0.000001f || dt <= 0f)
            {
                to = from;
                return IsSafeWaterPosition(from);
            }

            var step = new Vector3(_velocityXZ.x, 0f, _velocityXZ.y) * dt;

            for (var i = 0; i < 8; i++)
            {
                var t = 1f - i * 0.125f;
                var candidate = from + step * t;

                if (!IsSafeWaterPosition(candidate))
                {
                    continue;
                }

                if (_bodyShape.IsValid)
                {
                    if (FloatingGroundCollision.TryAdvancePosition(
                            from,
                            candidate,
                            transform.rotation,
                            _bodyShape,
                            groundMask,
                            out var cleared))
                    {
                        to = cleared;
                        return true;
                    }
                }
                else
                {
                    to = candidate;
                    return true;
                }
            }

            to = from;
            return false;
        }

        private bool IsSafeWaterPosition(Vector3 worldPos)
        {
            if (!WaterCarryClamp.ShouldUseWaterSurfaceAt(worldPos, groundMask, GetGroundProbeRadius(), out _))
            {
                return false;
            }

            if (_bodyShape.IsValid
                && FloatingGroundCollision.HasGroundOverlap(worldPos, transform.rotation, _bodyShape, groundMask))
            {
                return false;
            }

            return true;
        }

        private Vector3 ClampToAnchorRadius(Vector3 worldPos)
        {
            if (maxRadius <= 0.01f)
            {
                return worldPos;
            }

            var offset = new Vector2(worldPos.x - _anchorWorld.x, worldPos.z - _anchorWorld.z);
            var dist = offset.magnitude;
            if (dist <= maxRadius || dist < 0.0001f)
            {
                return worldPos;
            }

            var clamped = offset / dist * maxRadius;
            var candidate = new Vector3(_anchorWorld.x + clamped.x, worldPos.y, _anchorWorld.z + clamped.y);
            if (IsSafeWaterPosition(candidate))
            {
                return candidate;
            }

            // If the exact circle point is invalid, walk back towards anchor.
            for (var i = 0; i < 6; i++)
            {
                var t = 1f - (i + 1) / 6f;
                var back = new Vector3(
                    _anchorWorld.x + clamped.x * t,
                    worldPos.y,
                    _anchorWorld.z + clamped.y * t);
                if (IsSafeWaterPosition(back))
                {
                    return back;
                }
            }

            return _anchorWorld;
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos)
            {
                return;
            }

            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
            var anchor = Application.isPlaying ? _anchorWorld : transform.position;
            Gizmos.DrawWireSphere(anchor, maxRadius);

            Gizmos.color = new Color(1f, 0.7f, 0.1f, 0.6f);
            var pos = transform.position;
            var vel = Application.isPlaying ? _velocityXZ : Vector2.zero;
            Gizmos.DrawLine(pos, pos + new Vector3(vel.x, 0f, vel.y));
        }
    }
}

