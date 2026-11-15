using System.Collections;
using UnityEngine;

namespace PistolShrimp
{
    /// <summary>
    /// What the player feels in their hands when the claw fires. Add it to the
    /// PistolShrimpPlayer object. Three beats: a hard crack as the hammer lands,
    /// a decaying rumble as the jet leaves, and a softer thump when the bubble collapses.
    /// </summary>
    public class SnapHaptics : MonoBehaviour
    {
        [Tooltip("Empty = the PistolShrimpPlayer on this object.")]
        public PistolShrimpPlayer player;

        [Header("Crack")]
        [Range(0f, 1f)] public float crackStrength = 1f;
        public float crackTime = 0.05f;

        [Header("Rumble after the crack")]
        [Range(0f, 1f)] public float rumbleStrength = 0.6f;
        public float rumbleTime = 0.35f;

        [Header("Bubble collapse thump")]
        public bool collapseThump = true;
        [Tooltip("Seconds after the snap. Match it to how long the bullet usually flies.")]
        public float collapseDelay = 0.45f;
        [Range(0f, 1f)] public float collapseStrength = 0.45f;

        [Header("Other hand")]
        [Tooltip("How much of the shock the body carries to the pincer hand.")]
        [Range(0f, 1f)] public float otherHandShare = 0.3f;

        Coroutine _routine;

        void OnEnable()
        {
            if (player == null) player = GetComponent<PistolShrimpPlayer>();
            if (player != null) player.onSnap.AddListener(Play);
        }

        void OnDisable()
        {
            if (player != null) player.onSnap.RemoveListener(Play);
            if (_routine != null) StopCoroutine(_routine);
            _routine = null;
        }

        /// <summary>Plays the firing pattern. Hooked to the player's snap automatically.</summary>
        public void Play()
        {
            if (!isActiveAndEnabled) return;
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(Pattern());
        }

        IEnumerator Pattern()
        {
            bool right = player == null || player.snapperHand == SnapperHand.Right;

            // 1. Crack: one hard, short hit.
            Send(right, crackStrength, crackTime);
            yield return new WaitForSeconds(crackTime);

            // 2. Rumble: resend a weaker impulse every 30 ms so it fades out instead of stopping dead.
            float t = 0f;
            float lastSend = -1f;
            while (t < rumbleTime)
            {
                if (t - lastSend >= 0.03f)
                {
                    float k = 1f - t / Mathf.Max(0.01f, rumbleTime);
                    Send(right, rumbleStrength * k * k, 0.04f);
                    lastSend = t;
                }
                t += Time.deltaTime;
                yield return null;
            }

            // 3. Thump: the bubble collapsing out in the water.
            if (collapseThump)
            {
                float wait = collapseDelay - crackTime - rumbleTime;
                if (wait > 0f) yield return new WaitForSeconds(wait);
                Send(right, collapseStrength, 0.08f);
            }
            _routine = null;
        }

        void Send(bool snapperIsRight, float amplitude, float duration)
        {
            Impulse(snapperIsRight, amplitude, duration);
            if (otherHandShare > 0f) Impulse(!snapperIsRight, amplitude * otherHandShare, duration);
        }

        static void Impulse(bool right, float amplitude, float duration)
        {
            amplitude = Mathf.Clamp01(amplitude);
            if (amplitude < 0.02f) return;
            UnityEngine.XR.InputDevice device = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(
                right ? UnityEngine.XR.XRNode.RightHand : UnityEngine.XR.XRNode.LeftHand);
            if (device.isValid) device.SendHapticImpulse(0u, amplitude, duration);
        }
    }
}
