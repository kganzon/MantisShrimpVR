using System.Collections.Generic;
using UnityEngine;

namespace PistolShrimp
{
    public enum VibrationVoice { Rumble, Scuttle, Hum }

    /// <summary>
    /// Put this on anything in the world the shrimp should feel: prey, predators,
    /// currents, falling rocks. The faster it moves, the louder and higher it sounds.
    /// The sound is 3D, so the player locates things by ear instead of by sight.
    /// Works with no audio files: a placeholder loop is synthesized per voice.
    /// Drop your own loop (for example one exported from Satie) into Custom Clip to replace it.
    /// </summary>
    public class VibrationEmitter : MonoBehaviour
    {
        public static readonly List<VibrationEmitter> All = new List<VibrationEmitter>();

        [Header("Sound")]
        public VibrationVoice voice = VibrationVoice.Rumble;
        [Tooltip("Optional. A seamless loop that replaces the built-in placeholder.")]
        public AudioClip customClip;
        [Range(0f, 1f)] public float maxVolume = 0.9f;
        public float pitchWhenStill = 0.75f;
        public float pitchWhenFast = 1.35f;
        [Tooltip("Beyond this distance the shrimp senses nothing from this object.")]
        public float range = 12f;

        [Header("What counts as vibration")]
        [Tooltip("Vibration this object gives off even when it does not move (0 to 1). Use for a current or a burrowing animal.")]
        [Range(0f, 1f)] public float restingLevel = 0f;
        [Tooltip("Speed in m/s at which the vibration is at full strength.")]
        public float speedForFull = 1.5f;
        public float attackTime = 0.08f;
        public float releaseTime = 0.5f;

        /// <summary>Current vibration strength, 0 to 1.</summary>
        public float Level { get { return _level; } }

        AudioSource _source;
        Vector3 _lastPos;
        float _level;
        float _pulse;

        static readonly Dictionary<VibrationVoice, AudioClip> Placeholders = new Dictionary<VibrationVoice, AudioClip>();

        /// <summary>One-off jolt on top of the movement, for a footstep, a bite, a rock landing. strength 0 to 1.</summary>
        public void Pulse(float strength)
        {
            _pulse = Mathf.Max(_pulse, Mathf.Clamp01(strength));
        }

        void Awake()
        {
            _source = GetComponent<AudioSource>();
            if (_source == null) _source = gameObject.AddComponent<AudioSource>();
            _source.clip = customClip != null ? customClip : Placeholder(voice);
            _source.loop = true;
            _source.playOnAwake = false;
            _source.spatialBlend = 1f;          // fully 3D
            _source.spatialize = true;          // uses the HRTF spatializer if the project has one selected
            _source.dopplerLevel = 0f;
            _source.rolloffMode = AudioRolloffMode.Linear;
            _source.minDistance = 0.3f;
            _source.maxDistance = range;
            _source.volume = 0f;
        }

        void OnEnable()
        {
            All.Add(this);
            _lastPos = transform.position;
            if (_source != null)
            {
                // Random start so several emitters with the same voice do not phase-lock.
                if (_source.clip != null) _source.time = Random.value * _source.clip.length * 0.99f;
                _source.Play();
            }
        }

        void OnDisable()
        {
            All.Remove(this);
            if (_source != null) _source.Stop();
        }

        void Update()
        {
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            float speed = (transform.position - _lastPos).magnitude / dt;
            _lastPos = transform.position;

            float target = Mathf.Clamp01(restingLevel + speed / Mathf.Max(0.01f, speedForFull) + _pulse);
            _pulse = Mathf.MoveTowards(_pulse, 0f, dt / 0.25f);

            float time = target > _level ? attackTime : releaseTime;
            _level = Mathf.MoveTowards(_level, target, dt / Mathf.Max(0.01f, time));

            _source.maxDistance = range;
            _source.volume = _level * maxVolume;
            _source.pitch = Mathf.Lerp(pitchWhenStill, pitchWhenFast, _level);
        }

        // ------------------------------------------------------------------ placeholder loops
        static AudioClip Placeholder(VibrationVoice v)
        {
            AudioClip clip;
            if (Placeholders.TryGetValue(v, out clip) && clip != null) return clip;

            const int rate = 44100;
            const float seconds = 2f;                    // every frequency below is a multiple of 0.5 Hz, so the loop is seamless
            int n = (int)(rate * seconds);
            float[] data = new float[n];
            System.Random rng = new System.Random(11 + (int)v);
            const float tau = Mathf.PI * 2f;

            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float s = 0f;
                if (v == VibrationVoice.Rumble)
                {
                    // Low body movement, pitched up into what a headset speaker can reproduce.
                    float tremolo = 0.65f + 0.35f * Mathf.Sin(tau * 6f * t);
                    s = (Mathf.Sin(tau * 80f * t) * 0.55f + Mathf.Sin(tau * 120f * t) * 0.3f + Mathf.Sin(tau * 160f * t) * 0.15f) * tremolo;
                }
                else if (v == VibrationVoice.Scuttle)
                {
                    // Many small legs: a fast train of short ticks.
                    float phase = (t * 14f) % 1f;
                    float env = Mathf.Exp(-phase * 22f);
                    float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                    s = (noise * 0.5f + Mathf.Sin(tau * 1400f * t) * 0.5f) * env;
                }
                else
                {
                    // Steady field with a slow beat, for currents or something large and calm.
                    s = Mathf.Sin(tau * 220f * t) * 0.45f + Mathf.Sin(tau * 223f * t) * 0.45f + Mathf.Sin(tau * 330f * t) * 0.1f;
                }
                data[i] = s * 0.8f;
            }

            clip = AudioClip.Create("Vibration_" + v, n, 1, rate, false);
            clip.SetData(data, 0);
            Placeholders[v] = clip;
            return clip;
        }
    }
}
