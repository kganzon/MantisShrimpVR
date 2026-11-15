using System.Collections.Generic;
using UnityEngine;

namespace PistolShrimp
{
    /// <summary>
    /// The water jet fired by the snapper claw. Lives in world space (never parented
    /// to the claw). Leaves the muzzle fast, loses speed to water drag, and ends in a
    /// cavitation pop either on impact or when it runs out of speed.
    /// </summary>
    public class WaterBullet : MonoBehaviour
    {
        [Header("Flight")]
        public float launchSpeed = 28f;
        [Tooltip("Water drag. Higher = shorter range. Range is roughly (launchSpeed - popBelowSpeed) / drag metres.")]
        public float drag = 3.5f;
        public float popBelowSpeed = 3f;
        public float maxLifetime = 1.6f;
        public float radius = 0.04f;
        public LayerMask hitMask = ~0;

        [Header("Shockwave")]
        public float shockRadius = 0.5f;
        public float shockImpulse = 3f;
        public GameObject popPrefab;

        [Header("Visuals")]
        public Transform visual;
        public ParticleSystem trail;

        Vector3 _velocity;
        Vector3 _visualScale = Vector3.one;
        float _age;
        bool _launched;
        bool _dead;
        GameObject _source;
        Transform _ignoreA;
        Transform _ignoreB;

        /// <summary>Called by the shrimp right after spawning. ignoreA/ignoreB are hierarchies the jet passes through (the shrimp, the XR rig).</summary>
        public void Launch(Vector3 direction, GameObject source, Transform ignoreA, Transform ignoreB)
        {
            if (direction.sqrMagnitude < 0.0001f) direction = transform.forward;
            direction.Normalize();
            _velocity = direction * launchSpeed * WorldScale;
            _source = source;
            _ignoreA = ignoreA;
            _ignoreB = ignoreB;
            _launched = true;
            transform.rotation = Quaternion.LookRotation(direction);
            if (visual != null) _visualScale = visual.localScale;
        }

        float WorldScale
        {
            get { return Mathf.Max(0.0001f, transform.lossyScale.x); }
        }

        bool Ignored(Transform t)
        {
            if (t == null) return false;
            if (t.IsChildOf(transform)) return true;
            if (_ignoreA != null && t.IsChildOf(_ignoreA)) return true;
            if (_ignoreB != null && t.IsChildOf(_ignoreB)) return true;
            return false;
        }

        void Update()
        {
            if (_dead) return;
            if (!_launched) Launch(transform.forward, null, null, null);

            float dt = Time.deltaTime;
            _age += dt;
            float scale = WorldScale;
            float speed = _velocity.magnitude;
            Vector3 dir = speed > 0.0001f ? _velocity / speed : transform.forward;
            float dist = speed * dt;

            // Sweep this frame's path so a fast jet cannot tunnel through thin colliders.
            RaycastHit[] hits = Physics.SphereCastAll(transform.position, radius * scale, dir, dist, hitMask, QueryTriggerInteraction.Ignore);
            bool found = false;
            RaycastHit best = new RaycastHit();
            float bestDist = float.MaxValue;
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].collider == null || Ignored(hits[i].collider.transform)) continue;
                if (hits[i].distance < bestDist)
                {
                    bestDist = hits[i].distance;
                    best = hits[i];
                    found = true;
                }
            }

            if (found)
            {
                // distance 0 means the sweep started inside the collider: Unity reports no usable point then.
                Vector3 point = best.distance > 0f ? best.point : transform.position;
                Vector3 normal = best.distance > 0f ? best.normal : -dir;
                transform.position = point;
                Pop(point, normal, dir, best.collider);
                return;
            }

            transform.position += dir * dist;
            _velocity *= Mathf.Exp(-drag * dt);

            if (visual != null)
            {
                // The jet is long and thin when fast, and rounds off into a bubble as it slows.
                float k = Mathf.Clamp01(_velocity.magnitude / (launchSpeed * scale));
                visual.localScale = new Vector3(
                    _visualScale.x * Mathf.Lerp(1.5f, 1f, k),
                    _visualScale.y * Mathf.Lerp(1.5f, 1f, k),
                    _visualScale.z * Mathf.Lerp(0.45f, 1f, k));
            }

            if (_age >= maxLifetime || _velocity.magnitude <= popBelowSpeed * scale)
            {
                Pop(transform.position, -dir, dir, null);
            }
        }

        void Pop(Vector3 point, Vector3 normal, Vector3 travelDir, Collider directCollider)
        {
            if (_dead) return;
            _dead = true;
            float scale = WorldScale;

            if (popPrefab != null)
            {
                Quaternion rot = normal.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(normal) : Quaternion.identity;
                GameObject pop = Instantiate(popPrefab, point, rot);
                pop.transform.localScale = pop.transform.localScale * scale;
            }

            HashSet<ISnapHittable> told = new HashSet<ISnapHittable>();
            if (directCollider != null)
            {
                ISnapHittable directTarget = directCollider.GetComponentInParent<ISnapHittable>();
                if (directTarget != null)
                {
                    told.Add(directTarget);
                    directTarget.OnSnapHit(MakeHit(point, normal, travelDir, 1f, true));
                }
            }

            float r = shockRadius * scale;
            Collider[] near = Physics.OverlapSphere(point, r, hitMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < near.Length; i++)
            {
                Collider c = near[i];
                if (c == null || Ignored(c.transform)) continue;

                Rigidbody body = c.attachedRigidbody;
                if (body != null && !body.isKinematic)
                {
                    body.AddExplosionForce(shockImpulse, point, r, 0f, ForceMode.Impulse);
                }

                ISnapHittable target = c.GetComponentInParent<ISnapHittable>();
                if (target == null || !told.Add(target)) continue;
                float d = Vector3.Distance(point, c.bounds.ClosestPoint(point)); // bounds: safe for concave mesh colliders
                float falloff = Mathf.Clamp01(1f - d / Mathf.Max(r, 0.0001f));
                target.OnSnapHit(MakeHit(point, normal, travelDir, falloff, false));
            }

            if (trail != null)
            {
                // Let the bubbles already in the water finish instead of vanishing with the jet.
                trail.transform.SetParent(null, true);
                trail.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                Destroy(trail.gameObject, 2f);
            }
            Destroy(gameObject);
        }

        SnapHit MakeHit(Vector3 point, Vector3 normal, Vector3 dir, float strength, bool direct)
        {
            SnapHit hit = new SnapHit();
            hit.point = point;
            hit.normal = normal;
            hit.direction = dir;
            hit.strength = strength;
            hit.direct = direct;
            hit.source = _source;
            return hit;
        }
    }
}
