using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace PistolShrimp
{
    /// <summary>
    /// Example of something in the world that reacts to a snap. Put it on any object
    /// with a collider. Copy it or replace it with your own ISnapHittable.
    /// </summary>
    public class SnapTarget : MonoBehaviour, ISnapHittable
    {
        [Tooltip("Total snap strength this target can take before it is stunned. A direct hit counts 1.")]
        public float toughness = 2f;
        public float punchScale = 1.25f;
        public UnityEvent onHit;
        public UnityEvent onStunned;

        float _damage;
        bool _stunned;
        Vector3 _baseScale;
        Coroutine _punch;

        void Awake()
        {
            _baseScale = transform.localScale;
        }

        public void OnSnapHit(SnapHit hit)
        {
            _damage += hit.strength;
            if (onHit != null) onHit.Invoke();
            if (_punch != null) StopCoroutine(_punch);
            _punch = StartCoroutine(Punch());
            if (!_stunned && _damage >= toughness)
            {
                _stunned = true;
                if (onStunned != null) onStunned.Invoke();
            }
        }

        IEnumerator Punch()
        {
            float t = 0f;
            const float time = 0.18f;
            while (t < time)
            {
                t += Time.deltaTime;
                float k = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / time));
                transform.localScale = _baseScale * Mathf.Lerp(1f, punchScale, k);
                yield return null;
            }
            transform.localScale = _baseScale;
            _punch = null;
        }
    }
}
