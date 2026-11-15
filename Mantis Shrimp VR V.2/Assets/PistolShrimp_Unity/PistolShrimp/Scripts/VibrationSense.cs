using System.Collections;
using UnityEngine;

namespace PistolShrimp
{
    /// <summary>
    /// The shrimp's side of the vibration sense. Add it to the PistolShrimpPlayer object.
    ///  - Murk: thick fog so the eyes are nearly useless and the ears take over.
    ///  - Antennae: vibrations to the left buzz the left controller, to the right the right one.
    ///  - Snap echo: the player's own snap spreads outward and every emitter it reaches answers
    ///    with a short burst of its own voice. Near things answer first, far things later.
    /// The sound itself comes from the VibrationEmitter on each world object.
    /// </summary>
    public class VibrationSense : MonoBehaviour
    {
        [Header("Poor eyesight")]
        public bool murk = true;
        public Color murkColor = new Color(0.02f, 0.09f, 0.12f, 1f);
        [Tooltip("Higher = you see less far. 0.35 leaves about 3 m of visibility.")]
        public float murkDensity = 0.35f;

        [Header("Antennae (controller haptics)")]
        public bool antennaHaptics = true;
        [Range(0f, 1f)] public float maxHaptic = 0.35f;

        [Header("Snap echo")]
        public bool snapEcho = false;
        [Tooltip("How far the snap's pressure wave reaches.")]
        public float echoRange = 10f;
        [Tooltip("Metres per second. Slow on purpose so the gap between near and far answers is easy to hear.")]
        public float echoSpeed = 12f;
        [Range(0f, 1f)] public float echoStrength = 1f;
        [Tooltip("Empty = the PistolShrimpPlayer on this object.")]
        public PistolShrimpPlayer player;

        [Tooltip("Empty = Camera.main.")]
        public Transform head;

        /// <summary>Sum of everything felt right now, 0 to 1.</summary>
        public float TotalLevel { get; private set; }
        /// <summary>World direction of the strongest vibration, or zero if there is none.</summary>
        public Vector3 StrongestDirection { get; private set; }

        bool _saved;
        bool _oldFog;
        Color _oldFogColor;
        FogMode _oldFogMode;
        float _oldFogDensity;
        Camera _cam;
        CameraClearFlags _oldClear;
        Color _oldBackground;
        float _timer;

        void OnEnable()
        {
            if (player == null) player = GetComponent<PistolShrimpPlayer>();
            if (player != null) player.onSnap.AddListener(OnPlayerSnap);
            if (!murk) return;
            _oldFog = RenderSettings.fog;
            _oldFogColor = RenderSettings.fogColor;
            _oldFogMode = RenderSettings.fogMode;
            _oldFogDensity = RenderSettings.fogDensity;
            _saved = true;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = murkColor;
            RenderSettings.fogDensity = murkDensity;
        }

        void OnDisable()
        {
            if (player != null) player.onSnap.RemoveListener(OnPlayerSnap);
            StopAllCoroutines();
            if (_saved)
            {
                RenderSettings.fog = _oldFog;
                RenderSettings.fogColor = _oldFogColor;
                RenderSettings.fogMode = _oldFogMode;
                RenderSettings.fogDensity = _oldFogDensity;
                _saved = false;
            }
            if (_cam != null)
            {
                _cam.clearFlags = _oldClear;
                _cam.backgroundColor = _oldBackground;
                _cam = null;
            }
        }

        void Update()
        {
            if (head == null && Camera.main != null) head = Camera.main.transform;
            if (head == null) return;

            if (murk)
            {
                // The Brightness option lightens or darkens the murk along with the lights.
                Color murkNow = murkColor * ShrimpSettings.Brightness;
                murkNow.a = 1f;
                RenderSettings.fogColor = murkNow;
                RenderSettings.fogDensity = murkDensity;
                if (_cam == null)
                {
                    // The background must match the fog or distant objects show as silhouettes against the sky.
                    _cam = head.GetComponent<Camera>();
                    if (_cam != null)
                    {
                        _oldClear = _cam.clearFlags;
                        _oldBackground = _cam.backgroundColor;
                        _cam.clearFlags = CameraClearFlags.SolidColor;
                    }
                }
                if (_cam != null) _cam.backgroundColor = murkNow;
            }

            float left = 0f, right = 0f, total = 0f, best = 0f;
            Vector3 bestDir = Vector3.zero;
            for (int i = 0; i < VibrationEmitter.All.Count; i++)
            {
                VibrationEmitter e = VibrationEmitter.All[i];
                if (e == null) continue;
                Vector3 to = e.transform.position - head.position;
                float d = to.magnitude;
                float felt = e.Level * Mathf.Clamp01(1f - d / Mathf.Max(0.01f, e.range));
                if (felt <= 0.001f) continue;
                Vector3 dir = d > 0.0001f ? to / d : head.forward;
                float side = Vector3.Dot(head.right, dir);          // -1 = fully left, +1 = fully right
                left += felt * (0.5f - 0.5f * side);
                right += felt * (0.5f + 0.5f * side);
                total += felt;
                if (felt > best) { best = felt; bestDir = dir; }
            }
            TotalLevel = Mathf.Clamp01(total);
            StrongestDirection = bestDir;

            if (!antennaHaptics) return;
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = 0.05f;
            Buzz(UnityEngine.XR.XRNode.LeftHand, left);
            Buzz(UnityEngine.XR.XRNode.RightHand, right);
        }

        void OnPlayerSnap()
        {
            if (!snapEcho) return;
            Vector3 origin = transform.position;
            if (player != null && player.muzzle != null) origin = player.muzzle.position;
            SendPing(origin);
        }

        /// <summary>Sends a pressure wave from a point. Every emitter in range answers after a delay that grows with distance.</summary>
        public void SendPing(Vector3 origin)
        {
            for (int i = 0; i < VibrationEmitter.All.Count; i++)
            {
                VibrationEmitter e = VibrationEmitter.All[i];
                if (e == null) continue;
                float d = Vector3.Distance(origin, e.transform.position);
                if (d > echoRange) continue;
                float strength = echoStrength * Mathf.Lerp(1f, 0.25f, d / Mathf.Max(0.01f, echoRange));
                StartCoroutine(Answer(e, d / Mathf.Max(0.1f, echoSpeed), strength));
            }
        }

        IEnumerator Answer(VibrationEmitter e, float delay, float strength)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            if (e != null && e.isActiveAndEnabled) e.Pulse(strength);
        }

        void Buzz(UnityEngine.XR.XRNode node, float level)
        {
            if (!ShrimpSettings.Vibration) return;
            float amplitude = Mathf.Clamp01(level) * maxHaptic;
            if (amplitude < 0.02f) return;
            UnityEngine.XR.InputDevice device = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(node);
            if (device.isValid) device.SendHapticImpulse(0u, amplitude, 0.06f);
        }
    }
}
