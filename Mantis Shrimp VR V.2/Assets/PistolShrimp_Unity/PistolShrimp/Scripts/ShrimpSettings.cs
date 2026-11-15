using UnityEngine;

namespace PistolShrimp
{
    /// <summary>
    /// Player options shared by every script: volume, vibration, brightness and skin.
    /// Saved on the device, so they survive closing the app.
    /// </summary>
    public static class ShrimpSettings
    {
        public static float Volume = 1f;          // 0 to 1, master volume for everything
        public static float MusicVolume = 0.5f;   // 0 to 1, background music only
        public static bool Vibration = true;
        public static float Brightness = 1f;      // 0.5 to 1.5
        public static int Skin = 0;
        public static int Difficulty = 0;         // 0 easy (default), 1 normal, 2 hard
        public static bool LeftHanded = false;    // which hand wears the big snapper claw

        public static readonly string[] DifficultyNames = { "EASY", "NORMAL", "HARD" };
        /// <summary>How fast the food bar drains compared with normal.</summary>
        public static float HungerScale { get { return Difficulty == 0 ? 0.6f : (Difficulty == 2 ? 1.6f : 1f); } }
        /// <summary>How thick the water is compared with normal. Hard is closest to how little the animal sees.</summary>
        public static float MurkScale { get { return Difficulty == 0 ? 0.45f : (Difficulty == 2 ? 2.2f : 0.7f); } }

        static bool _loaded;

        public static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            Volume = PlayerPrefs.GetFloat("shrimp.volume", 1f);
            Vibration = PlayerPrefs.GetInt("shrimp.vibration", 1) == 1;
            MusicVolume = PlayerPrefs.GetFloat("shrimp.music", 0.5f);
            Brightness = PlayerPrefs.GetFloat("shrimp.brightness", 1f);
            Skin = PlayerPrefs.GetInt("shrimp.skin", 0);
            Difficulty = Mathf.Clamp(PlayerPrefs.GetInt("shrimp.difficulty2", 0), 0, 2);
            LeftHanded = PlayerPrefs.GetInt("shrimp.lefthanded", 0) == 1;
            AudioListener.volume = Volume;
        }

        public static void Save()
        {
            PlayerPrefs.SetFloat("shrimp.volume", Volume);
            PlayerPrefs.SetInt("shrimp.vibration", Vibration ? 1 : 0);
            PlayerPrefs.SetFloat("shrimp.music", MusicVolume);
            PlayerPrefs.SetFloat("shrimp.brightness", Brightness);
            PlayerPrefs.SetInt("shrimp.skin", Skin);
            PlayerPrefs.SetInt("shrimp.difficulty2", Difficulty);
            PlayerPrefs.SetInt("shrimp.lefthanded", LeftHanded ? 1 : 0);
            PlayerPrefs.Save();
            AudioListener.volume = Volume;
        }

        /// <summary>One place for controller vibration, so the Vibration option switches all of it.</summary>
        public static void Haptic(bool rightHand, float amplitude, float duration)
        {
            if (!Vibration) return;
            amplitude = Mathf.Clamp01(amplitude);
            if (amplitude < 0.02f) return;
            UnityEngine.XR.InputDevice device = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(
                rightHand ? UnityEngine.XR.XRNode.RightHand : UnityEngine.XR.XRNode.LeftHand);
            if (device.isValid) device.SendHapticImpulse(0u, amplitude, duration);
        }
    }
}
