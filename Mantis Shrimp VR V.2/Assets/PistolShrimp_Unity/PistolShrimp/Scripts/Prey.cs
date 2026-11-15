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
        /// <summary>Raised whenever any animal is eaten. The hunger system listens to this.</summary>
        public static event System.Action<Prey> Eaten;

        [Header("Attack")]
        [Tooltip("A direct hit removes 1. A near miss removes less. 1 = dies to one direct hit.")]
        public float health = 1f;
        [Tooltip("On death, switch off the other scripts and animators on this object so it stops swimming or walking.")]
        public bool stopOtherScriptsOnDeath = true;
        [Tooltip("On death, detach from its parent, in case the parent is what moves it.")]
        public bool detachOnDeath = true;

        [Tooltip("Multiplies the damage of every hit. 0.5 = it takes twice as many hits.")]
        public float damageMultiplier = 1f;
        [Tooltip("Show a small health bar above the animal once it has been hurt.")]
        public bool showHealthBar = true;
        [Tooltip("How fast the dead animal sinks, in metres per second. 0 = it hangs in the water.")]
        public float sinkSpeed = 0.6f;

        [Header("Eat")]
        [Tooltip("How much of the player's food bar this animal refills (the bar holds 100).")]
        public float nutrition = 25f;
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
        float _maxHealth;
        Vector3 _baseScale;
        Renderer[] _renderers;
        MaterialPropertyBlock _block;
        Coroutine _hitRoutine;
        Transform _bar;
        Transform _barFill;
        Renderer _barFillRenderer;

        static readonly Color HitTint = new Color(1f, 0.25f, 0.2f, 1f);
        static readonly Color DeadTint = new Color(0.45f, 0.47f, 0.5f, 1f);

        void Awake()
        {
            // The water bullet and the pincer both find things through colliders.
            if (GetComponentInChildren<Collider>() == null) AddFittedCollider();
            _maxHealth = Mathf.Max(0.01f, health);
            _baseScale = transform.localScale;
            _renderers = GetComponentsInChildren<Renderer>();
            _block = new MaterialPropertyBlock();
        }

        void OnDestroy()
        {
            if (_bar != null) Destroy(_bar.gameObject);
        }

        // ------------------------------------------------------------------ feedback
        void Tint(Color color, bool on)
        {
            if (_renderers == null) return;
            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer r = _renderers[i];
                if (r == null) continue;
                if (!on) { r.SetPropertyBlock(null); continue; }
                r.GetPropertyBlock(_block);
                _block.SetColor("_BaseColor", color);
                _block.SetColor("_Color", color);
                r.SetPropertyBlock(_block);
            }
        }

        // Hit: a red flash and a quick flinch.
        IEnumerator HitAnimation()
        {
            Tint(HitTint, true);
            float t = 0f;
            const float time = 0.22f;
            while (t < time)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / time);
                float squash = Mathf.Sin(k * Mathf.PI);
                transform.localScale = new Vector3(
                    _baseScale.x * (1f + 0.28f * squash),
                    _baseScale.y * (1f - 0.22f * squash),
                    _baseScale.z * (1f + 0.28f * squash));
                if (k > 0.55f) Tint(HitTint, false);
                yield return null;
            }
            transform.localScale = _baseScale;
            Tint(HitTint, false);
            _hitRoutine = null;
        }

        // Death: three fading twitches, then the colour drains out of the body.
        IEnumerator DeathAnimation()
        {
            Tint(HitTint, true);
            float t = 0f;
            const float time = 0.75f;
            while (t < time && !_eaten)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / time);
                float twitch = Mathf.Sin(k * Mathf.PI * 6f) * (1f - k) * 0.22f;
                transform.localScale = new Vector3(
                    _baseScale.x * (1f + twitch),
                    _baseScale.y * (1f - twitch),
                    _baseScale.z * (1f + twitch));
                Tint(Color.Lerp(HitTint, DeadTint, k), true);
                yield return null;
            }
            if (!_eaten) transform.localScale = _baseScale;
            Tint(DeadTint, true);
        }

        void UpdateHealthBar()
        {
            bool want = showHealthBar && !_dead && health < _maxHealth;
            if (!want)
            {
                if (_bar != null) { Destroy(_bar.gameObject); _bar = null; }
                return;
            }
            if (_bar == null)
            {
                const float width = 0.3f, height = 0.035f;
                GameObject root = new GameObject("PreyHealthBar");
                _bar = root.transform;
                Transform back = ShrimpUI.Quad("Back", _bar, new Color(0.03f, 0.05f, 0.07f, 0.85f));
                back.localScale = new Vector3(width + 0.02f, height + 0.02f, 1f);
                back.localPosition = new Vector3(0f, 0f, 0.003f);
                GameObject pivot = new GameObject("FillPivot");
                pivot.transform.SetParent(_bar, false);
                pivot.transform.localPosition = new Vector3(-width * 0.5f, 0f, 0f);
                _barFill = pivot.transform;
                Transform fill = ShrimpUI.Quad("Fill", _barFill, Color.red);
                fill.localScale = new Vector3(width, height, 1f);
                fill.localPosition = new Vector3(width * 0.5f, 0f, 0f);
                _barFillRenderer = fill.GetComponent<Renderer>();
            }
            // Float above the animal and face the player.
            Collider col = GetComponentInChildren<Collider>();
            Vector3 top = col != null ? new Vector3(col.bounds.center.x, col.bounds.max.y, col.bounds.center.z) : transform.position;
            _bar.position = top + Vector3.up * 0.12f;
            if (_head != null) _bar.rotation = Quaternion.LookRotation(_bar.position - _head.position);
            float k2 = Mathf.Clamp01(health / _maxHealth);
            _barFill.localScale = new Vector3(Mathf.Max(0.0001f, k2), 1f, 1f);
            _barFillRenderer.material.color = Color.Lerp(new Color(0.9f, 0.15f, 0.1f), new Color(0.35f, 0.9f, 0.45f), k2);
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
            health -= hit.strength * damageMultiplier;
            if (onHit != null) onHit.Invoke();
            if (health <= 0f)
            {
                Die(hit);
            }
            else
            {
                if (_hitRoutine != null) StopCoroutine(_hitRoutine);
                _hitRoutine = StartCoroutine(HitAnimation());
            }
        }

        void Die(SnapHit hit)
        {
            _dead = true;
            if (_hitRoutine != null) { StopCoroutine(_hitRoutine); _hitRoutine = null; }
            transform.localScale = _baseScale;

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
            _baseScale = transform.localScale;      // detaching can change the local scale

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

            StartCoroutine(DeathAnimation());
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
            if (_head == null && Camera.main != null) _head = Camera.main.transform;
            UpdateHealthBar();
            if (!_dead || _eaten) return;
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
            if (Eaten != null) Eaten(this);
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
            ShrimpSettings.Haptic(node == UnityEngine.XR.XRNode.RightHand, 0.7f, 0.07f);
        }
    }
}
