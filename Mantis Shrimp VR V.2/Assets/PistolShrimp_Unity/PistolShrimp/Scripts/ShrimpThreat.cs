using UnityEngine;

namespace PistolShrimp
{
    /// <summary>
    /// Something that hunts the player: the fishing net. You cannot see it coming
    /// through the murk, but you can hear and feel it.
    ///  - While the game is running it drifts toward the player.
    ///  - It gives off a constant low vibration that gets louder as it nears,
    ///    and the controllers pulse faster the closer it is.
    ///  - If it reaches the player, the run ends.
    ///  - A snap stuns it and knocks it back, which buys time to escape.
    /// ShrimpGame adds this to the reef's net automatically. You can also add it by hand.
    /// </summary>
    public class ShrimpThreat : MonoBehaviour, ISnapHittable
    {
        [Tooltip("Metres per second. Keep it below the player's move speed so escape is possible.")]
        public float speed = 1.2f;
        [Tooltip("It only starts closing in once the player is within this distance.")]
        public float noticeRange = 18f;
        [Tooltip("How close the middle of the net must get to the player's head to catch them.")]
        public float catchRadius = 1f;
        [Tooltip("Seconds it stays stunned after being hit by a snap.")]
        public float stunSeconds = 4f;
        [Tooltip("Metres it is thrown back by a direct hit.")]
        public float knockback = 2.5f;
        [Tooltip("Inside this distance the controllers start to pulse.")]
        public float dreadRange = 9f;

        float _stun;
        float _pulse;
        Transform _head;
        VibrationEmitter _emitter;
        Vector3 _localCentre;     // middle of the net opening, in this object's space

        /// <summary>World position of the middle of the net. This is the point that hunts the player.</summary>
        public Vector3 Centre { get { return transform.TransformPoint(_localCentre); } }

        void Start()
        {
            // The object's own position can be far from the visible net (this one has its pivot on the
            // seabed and the ring 1.7 m above it). Hunt with the middle of what the player actually sees:
            // the average of all the visible pieces, which a single long handle barely shifts.
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            Vector3 centre = transform.position;
            float radius = 1f;
            if (renderers.Length > 0)
            {
                Vector3 sum = Vector3.zero;
                for (int i = 0; i < renderers.Length; i++) sum += renderers[i].bounds.center;
                centre = sum / renderers.Length;
                float far = 0f;
                for (int i = 0; i < renderers.Length; i++)
                {
                    Vector3 d = renderers[i].bounds.center - centre;
                    d.y = 0f;
                    far = Mathf.Max(far, d.magnitude);
                }
                radius = Mathf.Max(0.5f, far);
            }
            _localCentre = transform.InverseTransformPoint(centre);

            if (GetComponentInChildren<Collider>() == null)
            {
                // Needs a collider so the water jet can hit it.
                SphereCollider sphere = gameObject.AddComponent<SphereCollider>();
                float s = Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.x));
                sphere.center = _localCentre;
                sphere.radius = radius / s;
            }

            _emitter = GetComponent<VibrationEmitter>();
            if (_emitter == null)
            {
                _emitter = gameObject.AddComponent<VibrationEmitter>();
                _emitter.voice = VibrationVoice.Rumble;
                _emitter.restingLevel = 0.45f;
                _emitter.pitchWhenStill = 0.5f;
                _emitter.pitchWhenFast = 0.8f;
                _emitter.range = 24f;
            }
        }

        void Update()
        {
            ShrimpGame game = ShrimpGame.Instance;
            if (game == null || !game.Playing) return;
            if (_head == null && Camera.main != null) _head = Camera.main.transform;
            if (_head == null) return;

            float dt = Time.deltaTime;
            Vector3 to = _head.position - Centre;
            float distance = to.magnitude;

            if (_stun > 0f)
            {
                _stun -= dt;
                return;
            }

            if (distance <= catchRadius)
            {
                game.EndCaught();
                return;
            }

            if (distance <= noticeRange)
            {
                Vector3 step = to.normalized * speed * dt;
                step.y *= 0.5f;                       // it sinks and rises more slowly than it drifts
                transform.position += step;

                // Turn the opening toward the player.
                Vector3 flat = to;
                flat.y = 0f;
                if (flat.sqrMagnitude > 0.01f)
                {
                    Quaternion face = Quaternion.LookRotation(flat.normalized);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, face, 60f * dt);
                }
            }

            // Dread: slow, soft pulses far away, quick and strong when it is on top of you.
            if (distance < dreadRange)
            {
                float k = 1f - Mathf.Clamp01(distance / Mathf.Max(0.01f, dreadRange));
                _pulse -= dt;
                if (_pulse <= 0f)
                {
                    _pulse = Mathf.Lerp(1.1f, 0.22f, k);
                    float amplitude = Mathf.Lerp(0.15f, 0.7f, k);
                    ShrimpSettings.Haptic(true, amplitude, 0.06f);
                    ShrimpSettings.Haptic(false, amplitude, 0.06f);
                }
            }
        }

        public void OnSnapHit(SnapHit hit)
        {
            _stun = Mathf.Max(_stun, stunSeconds * Mathf.Clamp01(hit.strength + 0.25f));
            Vector3 push = hit.direction;
            push.y = 0f;
            if (push.sqrMagnitude > 0.0001f) transform.position += push.normalized * knockback * hit.strength;
            if (_emitter != null) _emitter.Pulse(1f);
        }
    }
}
