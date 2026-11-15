using UnityEngine;

namespace PistolShrimp
{
    /// <summary>
    /// Background music. Plays the clip named "ShrimpMusic" from a Resources folder on a loop.
    /// It survives scene reloads (Restart, Play Again), so the track never cuts out.
    /// Volume = Music setting, and the Audio setting scales it on top as the master volume.
    /// To use your own track, replace ShrimpMusic in PistolShrimp/Resources with a file of the same name.
    /// </summary>
    public class ShrimpMusic : MonoBehaviour
    {
        static ShrimpMusic _instance;

        AudioSource _source;
        float _level = 1f;        // extra fade on top of the setting, for endings
        float _target = 1f;

        /// <summary>Starts the music if it is not playing yet. Does nothing if there is no ShrimpMusic clip.</summary>
        public static void Ensure()
        {
            if (_instance != null) return;
            AudioClip clip = Resources.Load<AudioClip>("ShrimpMusic");
            if (clip == null)
            {
                Debug.Log("[Pistol Shrimp] No ShrimpMusic clip in a Resources folder, so no background music.");
                return;
            }
            GameObject go = new GameObject("ShrimpMusic");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<ShrimpMusic>();
            AudioSource s = go.AddComponent<AudioSource>();
            s.clip = clip;
            s.loop = true;
            s.playOnAwake = false;
            s.spatialBlend = 0f;          // music is not placed in the world
            s.priority = 0;               // never dropped when many sounds play at once
            s.dopplerLevel = 0f;
            s.volume = ShrimpSettings.MusicVolume;
            s.Play();
            _instance._source = s;
        }

        /// <summary>Fades the music toward a fraction of its set volume. 1 = full, 0.3 = quiet under an end screen.</summary>
        public static void FadeTo(float level)
        {
            if (_instance != null) _instance._target = Mathf.Clamp01(level);
        }

        void Update()
        {
            if (_source == null) return;
            _level = Mathf.MoveTowards(_level, _target, Time.unscaledDeltaTime * 0.6f);
            _source.volume = ShrimpSettings.MusicVolume * _level;
        }
    }
}
