using UnityEngine;

namespace PistolShrimp
{
    /// <summary>
    /// The end of a snap: a cavitation bubble swells, collapses, and throws out a
    /// shock ring. Spawned by WaterBullet, cleans itself up.
    /// </summary>
    public class CavitationPop : MonoBehaviour
    {
        [Tooltip("Holder of the bubble mesh. Scaled by this script.")]
        public Transform bubble;
        [Tooltip("Holder of the ring mesh. Scaled by this script.")]
        public Transform ring;
        public float duration = 0.38f;
        public float bubbleSize = 0.32f;
        public float ringSize = 1.2f;
        public float lifetime = 1.8f;
        public AudioSource audioSource;

        Renderer _bubbleRenderer;
        Renderer _ringRenderer;
        Material _bubbleMat;
        Material _ringMat;
        Color _bubbleColor = Color.white;
        Color _ringColor = Color.white;
        float _t;

        void Start()
        {
            if (bubble != null) _bubbleRenderer = bubble.GetComponentInChildren<Renderer>();
            if (ring != null) _ringRenderer = ring.GetComponentInChildren<Renderer>();
            if (_bubbleRenderer != null) { _bubbleMat = _bubbleRenderer.material; _bubbleColor = ReadColor(_bubbleMat); }
            if (_ringRenderer != null) { _ringMat = _ringRenderer.material; _ringColor = ReadColor(_ringMat); }
            if (audioSource != null)
            {
                audioSource.pitch = Random.Range(0.92f, 1.08f);
                audioSource.Play();
            }
            Apply(0f);
            Destroy(gameObject, lifetime);
        }

        void Update()
        {
            _t += Time.deltaTime;
            Apply(Mathf.Clamp01(_t / Mathf.Max(0.01f, duration)));
        }

        void Apply(float k)
        {
            if (bubble != null)
            {
                // Swell, then collapse faster than it grew.
                float s = Mathf.Sin(Mathf.PI * Mathf.Pow(k, 0.7f));
                bubble.localScale = Vector3.one * (bubbleSize * Mathf.Max(0f, s));
                if (_bubbleRenderer != null) _bubbleRenderer.enabled = k < 1f;
            }
            if (ring != null)
            {
                float e = 1f - (1f - k) * (1f - k);
                ring.localScale = Vector3.one * (ringSize * Mathf.Lerp(0.15f, 1f, e));
                if (_ringMat != null)
                {
                    Color c = _ringColor;
                    c.a = _ringColor.a * (1f - k);
                    WriteColor(_ringMat, c);
                }
                if (_ringRenderer != null) _ringRenderer.enabled = k < 1f;
            }
        }

        static Color ReadColor(Material m)
        {
            if (m.HasProperty("_BaseColor")) return m.GetColor("_BaseColor");
            if (m.HasProperty("_Color")) return m.GetColor("_Color");
            return Color.white;
        }

        static void WriteColor(Material m, Color c)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }

        void OnDestroy()
        {
            if (_bubbleMat != null) Destroy(_bubbleMat);
            if (_ringMat != null) Destroy(_ringMat);
        }
    }
}
