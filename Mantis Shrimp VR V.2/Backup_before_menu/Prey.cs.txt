using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace PistolShrimp
{
    /// <summary>
    /// Makes an animal huntable. Put it on each fish, crab or clam.
    ///  1. Attack: a snap that hits it (or pops close to it) takes health away.
    ///  2. Dead: it stops moving and drifts. It can now be picked up with the pincer claw.
    ///  3. Eat: bring the dead animal to your mouth (just below the headset) and it is eaten.
    /// Needs no setup: it adds its own collider and physics body if the object has none.
    /// </summary>
    public class Prey : MonoBehaviour, ISnapHittable
    {
        /// <summary>How many animals have been eaten this session.</summary>
        public static int EatenCount;

        [Header("Attack")]
        [Tooltip("A direct hit removes 1. A near miss removes less. 1 = dies to one direct hit.")]
        public float health = 1f;
        [Tooltip("On death, switch off the other scripts and animators on this object so it stops swimming or walking.")]
        public bool stopOtherScriptsOnDeath = true;
        [Tooltip("On death, detach from its parent, in case the parent is what moves it.")]
        public bool detachOnDeath = true;

        [Tooltip("How fast the dead animal sinks, in metres per second. 0 = it hangs in the water.")]
        public float sinkSpeed = 0.6f;

        [Header("Eat")]
        [Tooltip("How close to the mouth the dead animal has to be, in metres.")]
        public float mouthRadius = 0.28f;
        [Tooltip("Mouth position relative to the headset: a little below and in front of the eyes.")]
        public Vector3 mouthOffset = new Vector3(0f, -0.12f, 0.08f);
        public AudioClip eatSound;

        [Header("Events")]
        public UnityEvent onHit;
        public UnityEvent onKilled;
        public UnityEvent onEaten;

        public bool IsDead { get { return _dead; } }

        const float Damping = 2.5f;
        bool _dead;
        bool _eaten;
        float _deathHeight;
        Rigidbody _body;
        Transform _head;

        void Awake()
        {
            // The water bullet and the pincer both find things through colliders.
            if (GetComponentInChildren<Collider>() == null) AddFittedCollider();
        }

        void AddFittedCollider()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            BoxCollider box = gameObject.AddComponent<BoxCollider>();
            if (renderers.Length == 0) return;
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            Vector3 s = transform.lossyScale;
            box.center = transform.InverseTransformPoint(b.center);
            box.size = new Vector3(
                b.size.x / Mathf.Max(0.0001f, Mathf.Abs(s.x)),
                b.size.y / Mathf.Max(0.0001f, Mathf.Abs(s.y)),
                b.size.z / Mathf.Max(0.0001f, Mathf.Abs(s.z)));
        }

        public void OnSnapHit(SnapHit hit)
        {
            if (_dead) return;
            health -= hit.strength;
            if (onHit != null) onHit.Invoke();
            if (health <= 0f) Die(hit);
        }

        void Die(SnapHit hit)
        {
            _dead = true;

            if (stopOtherScriptsOnDeath)
            {
                Animator[] animators = GetComponentsInChildren<Animator>();
                for (int i = 0; i < animators.Length; i++) animators[i].enabled = false;
                MonoBehaviour[] scripts = GetComponents<MonoBehaviour>();
                for (int i = 0; i < scripts.Length; i++)
                {
                    if (scripts[i] != null && scripts[i] != this) scripts[i].enabled = false;
                }
            }
            if (detachOnDeath) transform.SetParent(null, true);

            // A physics body lets it drift from the shock and lets the pincer pick it up.
            _body = GetComponent<Rigidbody>();
            if (_body == null) _body = gameObject.AddComponent<Rigidbody>();
            _body.isKinematic = false;
            _body.useGravity = false;                 // sinking is handled in FixedUpdate, slower than a real fall
            _deathHeight = transform.position.y;
#if UNITY_6000_0_OR_NEWER
            _body.linearDamping = Damping;
            _body.angularDamping = 1.5f;
#else
            _body.drag = Damping;
            _body.angularDrag = 1.5f;
#endif
            _body.AddForce(hit.direction * 0.6f + Vector3.down * 0.15f, ForceMode.VelocityChange);
            _body.AddTorque(Random.onUnitSphere * 1.5f, ForceMode.VelocityChange);   // slow belly-up roll

            if (onKilled != null) onKilled.Invoke();
        }

        void FixedUpdate()
        {
            if (!_dead || _eaten || _body == null || _body.isKinematic || sinkSpeed <= 0f) return;
            // A steady pull against the water's drag settles at exactly sinkSpeed.
            _body.AddForce(Vector3.down * sinkSpeed * Damping, ForceMode.Acceleration);
            // Safety net: if there is no seabed collider to land on, do not fall forever.
            if (transform.position.y < _deathHeight - 60f) Destroy(gameObject);
        }

        void Update()
        {
            if (!_dead || _eaten) return;
            if (_head == null && Camera.main != null) _head = Camera.main.transform;
            if (_head == null) return;

            float scale = _head.lossyScale.x;
            Vector3 mouth = _head.TransformPoint(mouthOffset);
            Collider col = GetComponentInChildren<Collider>();
            Vector3 nearest = col != null ? col.bounds.ClosestPoint(mouth) : transform.position;
            if (Vector3.Distance(mouth, nearest) <= mouthRadius * scale) StartCoroutine(Eat(mouth));
        }

        IEnumerator Eat(Vector3 mouth)
        {
            _eaten = true;
            EatenCount++;
            if (eatSound != null) AudioSource.PlayClipAtPoint(eatSound, mouth);
            if (onEaten != null) onEaten.Invoke();

            // Three bites in both hands while it shrinks into the mouth.
            Vector3 startScale = transform.localScale;
            Vector3 startPos = transform.position;
            float t = 0f;
            const float time = 0.6f;
            int bites = 0;
            while (t < time)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / time);
                if (k * 3f >= bites)
                {
                    bites++;
                    Bite(UnityEngine.XR.XRNode.LeftHand);
                    Bite(UnityEngine.XR.XRNode.RightHand);
                }
                transform.localScale = startScale * (1f - k);
                if (_head != null) mouth = _head.TransformPoint(mouthOffset);
                transform.position = Vector3.Lerp(startPos, mouth, k);
                yield return null;
            }
            Destroy(gameObject);
        }

        static void Bite(UnityEngine.XR.XRNode node)
        {
            UnityEngine.XR.InputDevice device = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(node);
            if (device.isValid) device.SendHapticImpulse(0u, 0.7f, 0.07f);
        }
    }
}
