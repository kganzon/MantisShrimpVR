using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PistolShrimp
{
    /// <summary>
    /// Game state, hunger and the food bar. Creates itself when a scene with a
    /// PistolShrimpPlayer starts, so nothing has to be added by hand.
    /// Hunger drains slowly while playing. Eating a Prey refills it. At zero you starve.
    /// </summary>
    public class ShrimpGame : MonoBehaviour
    {
        public static ShrimpGame Instance { get; private set; }

        [Header("Hunger")]
        public float maxFood = 100f;
        [Tooltip("Seconds from a full bar to starving if you eat nothing.")]
        public float secondsToStarve = 150f;
        [Tooltip("Below this fraction the bar turns red and the controllers pulse like a heartbeat.")]
        [Range(0f, 1f)] public float hungryBelow = 0.25f;

        [Header("Ending")]
        [Tooltip("Seconds of play after which the run ends with 'You survived'. 0 = no time limit.")]
        public float surviveSeconds = 180f;
        [Tooltip("Meals after which the run ends with 'You survived'. 0 = no meal goal.")]
        public int mealsToWin = 0;

        [Header("Snap moment")]
        [Tooltip("Flash of light at the claw tip on every snap.")]
        public bool snapFlash = true;
        [Tooltip("The reef goes quiet for a moment after the crack, as if your ears were ringing.")]
        public bool snapDucksAudio = true;

        public float Food { get; private set; }
        public bool Playing { get; private set; }
        /// <summary>True once the run has ended, whatever the reason.</summary>
        public bool Over { get; private set; }
        public float PlayTime { get; private set; }
        public int Meals { get; private set; }
        public PistolShrimpPlayer Player { get; private set; }
        public ShrimpMenu Menu { get; private set; }

        Transform _head;
        Transform _bar;
        Transform _fill;
        Renderer _fillRenderer;
        TextMesh _label;
        TextMesh _timer;
        float _heartbeat;
        float _fogBase;
        VibrationSense _sense;
        float _murkBase;
        Transform _frozenRig;
        Vector3 _frozenPosition;
        Quaternion _frozenRotation;
        Coroutine _duck;

        Light _sun;
        float _sunBase;
        float _ambientBase;
        Color _ambientColorBase;
        bool _brightnessCached;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            // AfterSceneLoad only fires once per app launch, so also listen for reloads ("Try again").
            SceneManager.sceneLoaded += OnSceneLoaded;
            Spawn();
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Spawn();
        }

        static void Spawn()
        {
            if (Instance != null) return;
            PistolShrimpPlayer player = Object.FindAnyObjectByType<PistolShrimpPlayer>();
            if (player == null) return;                       // not a shrimp scene
            GameObject go = new GameObject("ShrimpGame");
            ShrimpGame game = go.AddComponent<ShrimpGame>();
            game.Player = player;
        }

        void Awake()
        {
            Instance = this;
            ShrimpSettings.Load();
            Food = maxFood;
            Prey.Eaten += OnPreyEaten;
        }

        void OnDestroy()
        {
            Prey.Eaten -= OnPreyEaten;
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            if (Player != null)
            {
                Player.SetColorway(ShrimpSettings.Skin);
                Player.onSnap.AddListener(OnSnap);
            }
            ApplyHandedness();
            ShrimpMusic.Ensure();
            ShrimpMusic.FadeTo(1f);           // back to full after an ending or restart
            AdoptThreats();
            BuildBoundary();
            HideRigRays();
            Invoke("HideRigRays", 1.5f);      // again once the rig has finished setting itself up
            GameObject menuGo = new GameObject("ShrimpMenu");
            Menu = menuGo.AddComponent<ShrimpMenu>();
            Menu.Init(this);
            Menu.Show();
        }

        // The reef's fishing net was built for the old desktop player. Hand it over to
        // ShrimpThreat, which hunts the VR player and can end the run.
        void AdoptThreats()
        {
            if (Object.FindAnyObjectByType<ShrimpThreat>() != null) return;   // set up by hand already
            System.Type old = System.Type.GetType("MantisShrimpVR.MantisNetChase, Assembly-CSharp");
            if (old == null) return;
            Object found = Object.FindAnyObjectByType(old);
            Behaviour chase = found as Behaviour;
            if (chase == null) return;
            chase.enabled = false;
            chase.gameObject.AddComponent<ShrimpThreat>();
        }

        // ------------------------------------------------------------------ handedness
        /// <summary>
        /// Puts the big snapper claw on the hand chosen in Options. The two bodies are mirror
        /// images, so this swaps the whole shrimp for the other prefab and carries the settings over.
        /// </summary>
        public void ApplyHandedness()
        {
            if (Player == null) return;
            bool wantLeft = ShrimpSettings.LeftHanded;
            bool isLeft = Player.snapperHand == SnapperHand.Left;
            if (wantLeft == isLeft) return;

            PistolShrimpPlayer prefab = Resources.Load<PistolShrimpPlayer>(wantLeft ? "PistolShrimpPlayer_LeftHanded" : "PistolShrimpPlayer_RightHanded");
            if (prefab == null)
            {
                Debug.LogWarning("[Pistol Shrimp] Could not find the other-handed shrimp prefab in a Resources folder.");
                return;
            }

            PistolShrimpPlayer old = Player;
            PistolShrimpPlayer now = Instantiate(prefab, old.transform.parent);
            now.name = prefab.name;
            now.transform.SetPositionAndRotation(old.transform.position, old.transform.rotation);
            now.transform.localScale = old.transform.localScale;

            // Keep whatever was tuned on the shrimp in the scene.
            now.clawPose = old.clawPose;
            now.followHead = old.followHead;
            now.eyeOffset = new Vector3(-old.eyeOffset.x, old.eyeOffset.y, old.eyeOffset.z);
            now.bodyTurnSmoothTime = old.bodyTurnSmoothTime;
            now.matchRigScale = old.matchRigScale;
            now.snapMode = old.snapMode;
            now.snapOpenAngle = old.snapOpenAngle;
            now.cockTime = old.cockTime;
            now.snapCloseTime = old.snapCloseTime;
            now.cooldown = old.cooldown;
            now.releaseThreshold = old.releaseThreshold;
            now.pullThreshold = old.pullThreshold;
            now.pinchOpenAngle = old.pinchOpenAngle;
            now.grabRadius = old.grabRadius;
            now.grabMask = old.grabMask;
            now.haptics = old.haptics;
            now.keyboardFallback = old.keyboardFallback;
            now.snap.positionOffset = Mirror(old.snap.positionOffset);
            now.snap.rotationOffset = MirrorAngles(old.snap.rotationOffset);
            now.pinch.positionOffset = Mirror(old.pinch.positionOffset);
            now.pinch.rotationOffset = MirrorAngles(old.pinch.rotationOffset);

            // Components that were added to the shrimp by hand (vignette, firing haptics) move across too.
            MonoBehaviour[] extras = old.GetComponents<MonoBehaviour>();
            for (int i = 0; i < extras.Length; i++)
            {
                MonoBehaviour e = extras[i];
                if (e == null || e is PistolShrimpPlayer) continue;
                MonoBehaviour copy = now.gameObject.AddComponent(e.GetType()) as MonoBehaviour;
                if (copy == null) continue;
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(e), copy);
                copy.enabled = e.enabled;
            }
            SnapHaptics haptic = now.GetComponent<SnapHaptics>();
            if (haptic != null) haptic.player = now;
            VibrationSense sense = now.GetComponent<VibrationSense>();
            if (sense != null) sense.player = now;

            old.onSnap.RemoveListener(OnSnap);
            now.onSnap.AddListener(OnSnap);
            Player = now;
            now.SetColorway(ShrimpSettings.Skin);
            Destroy(old.gameObject);
        }

        static Vector3 Mirror(Vector3 v) { return new Vector3(-v.x, v.y, v.z); }
        static Vector3 MirrorAngles(Vector3 e) { return new Vector3(e.x, -e.y, -e.z); }

        // ------------------------------------------------------------------ world fixes
        // Invisible walls around the seabed, so nobody walks or swims off the edge of the reef.
        void BuildBoundary()
        {
            GameObject seabed = GameObject.Find("Seabed");
            if (seabed == null) return;
            Renderer r = seabed.GetComponent<Renderer>();
            Collider c = seabed.GetComponent<Collider>();
            Bounds b;
            if (r != null) b = r.bounds;
            else if (c != null) b = c.bounds;
            else return;

            const float thick = 2f, height = 80f, inset = 0.6f;
            GameObject root = new GameObject("ReefBoundary");
            float y = b.max.y + height * 0.5f - 10f;
            float x0 = b.min.x + inset, x1 = b.max.x - inset, z0 = b.min.z + inset, z1 = b.max.z - inset;
            Wall(root, new Vector3(x1 + thick * 0.5f, y, b.center.z), new Vector3(thick, height, b.size.z + thick * 2f));
            Wall(root, new Vector3(x0 - thick * 0.5f, y, b.center.z), new Vector3(thick, height, b.size.z + thick * 2f));
            Wall(root, new Vector3(b.center.x, y, z1 + thick * 0.5f), new Vector3(b.size.x + thick * 2f, height, thick));
            Wall(root, new Vector3(b.center.x, y, z0 - thick * 0.5f), new Vector3(b.size.x + thick * 2f, height, thick));
        }

        static void Wall(GameObject root, Vector3 position, Vector3 size)
        {
            GameObject go = new GameObject("Wall");
            go.transform.SetParent(root.transform, false);
            go.transform.position = position;
            go.AddComponent<BoxCollider>().size = size;
        }

        Transform Rig()
        {
            if (_head == null && Camera.main != null) _head = Camera.main.transform;
            return _head != null ? _head.root : null;
        }

        // The starter rig draws a teleport arc from the controller. The shrimp does not teleport and the
        // water jet does not follow that arc, so hide every ray the rig draws and switch teleporting off.
        void HideRigRays()
        {
            Transform rig = Rig();
            if (rig == null) return;
            LineRenderer[] lines = rig.GetComponentsInChildren<LineRenderer>(true);
            for (int i = 0; i < lines.Length; i++) lines[i].enabled = false;
            MonoBehaviour[] all = rig.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null) continue;
                string n = all[i].GetType().Name;
                if (n.Contains("LineVisual") || n.Contains("CurveVisual") || n.Contains("Reticle") || n == "TeleportationProvider")
                {
                    all[i].enabled = false;
                }
            }
        }

        // Once the run is over the player stays where they are. Only the claw still works, for the end screen.
        void FreezeMovement()
        {
            Transform rig = Rig();
            if (rig == null) return;
            Transform locomotion = rig.Find("Locomotion");
            if (locomotion != null) locomotion.gameObject.SetActive(false);
            // Belt and braces: whatever else might move the rig, hold it at this exact spot.
            _frozenRig = rig;
            _frozenPosition = rig.position;
            _frozenRotation = rig.rotation;
        }

        void LateUpdate()
        {
            if (Over && _frozenRig != null) _frozenRig.SetPositionAndRotation(_frozenPosition, _frozenRotation);
        }

        // ------------------------------------------------------------------ state
        public void StartPlaying()
        {
            if (Over)
            {
                // Fresh reef, fresh shrimp.
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                return;
            }
            Playing = true;
            SetLocomotion(true);
        }

        /// <summary>Stops the clock, the hunger and the net, and holds the player still while the menu is open.</summary>
        public void Pause()
        {
            Playing = false;
            if (!Over) SetLocomotion(false);
        }

        /// <summary>Throws the current run away and reloads the reef.</summary>
        public void Restart()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        void SetLocomotion(bool on)
        {
            Transform rig = Rig();
            if (rig == null) return;
            Transform locomotion = rig.Find("Locomotion");
            if (locomotion != null) locomotion.gameObject.SetActive(on);
        }

        void OnPreyEaten(Prey prey)
        {
            if (Over) return;
            Meals++;
            Food = Mathf.Min(maxFood, Food + (prey != null ? prey.nutrition : 25f));
            if (mealsToWin > 0 && Meals >= mealsToWin) End("YOU SURVIVED", FactGoby, false);
        }

        // ------------------------------------------------------------------ endings
        const string FactSnap = "A pistol shrimp's claw shuts so fast that it tears\na bubble out of the water. When the bubble collapses,\nthe crack is among the loudest sounds in the sea.";
        const string FactLight = "The collapsing bubble is so violent that for an instant\nit gives off a flash of light and heat of several\nthousand degrees. The shrimp hunts with it.";
        const string FactGoby = "Many pistol shrimp see poorly. Some share a burrow\nwith a goby fish that watches for danger while\nthe shrimp keeps a feeler on its tail.";

        public void EndStarved() { End("YOU STARVED", FactSnap, false); }
        public void EndCaught() { End("YOU WERE CAUGHT", "Tonight's special: shrimp fried rice.", true); }

        void End(string title, string fact, bool caught)
        {
            if (Over) return;
            Over = true;
            Playing = false;
            FreezeMovement();
            ShrimpMusic.FadeTo(caught ? 0f : 0.35f);   // silence for the net, quiet under the other endings
            ShrimpSettings.Haptic(true, 1f, 0.4f);
            ShrimpSettings.Haptic(false, 1f, 0.4f);
            int seconds = Mathf.FloorToInt(PlayTime);
            string stats = "SURVIVED  " + (seconds / 60) + ":" + (seconds % 60).ToString("00") + "      MEALS  " + Meals;
            if (Menu != null) Menu.ShowEnding(title, stats + "\n\n" + fact, caught);
        }

        // ------------------------------------------------------------------ snap moment
        void OnSnap()
        {
            if (snapFlash && Player != null && Player.muzzle != null) StartCoroutine(Flash(Player.muzzle));
            if (snapDucksAudio)
            {
                if (_duck != null) StopCoroutine(_duck);
                _duck = StartCoroutine(Duck());
            }
        }

        // A small burst of light at the claw tip, like the real bubble's flash. It stays
        // at the claw and never covers the whole view.
        IEnumerator Flash(Transform at)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "SnapFlash";
            Collider col = go.GetComponent<Collider>();
            if (col != null) col.enabled = false;
            Renderer r = go.GetComponent<Renderer>();
            Material m = ShrimpUI.FlatMaterial(new Color(0.85f, 0.97f, 1f, 0.9f));
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            float scale = at.lossyScale.x;
            float t = 0f;
            const float time = 0.14f;
            while (t < time)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / time);
                if (at != null) go.transform.position = at.position + at.forward * 0.04f * scale;
                go.transform.localScale = Vector3.one * Mathf.Lerp(0.05f, 0.42f, k) * scale;
                m.color = new Color(0.85f, 0.97f, 1f, 0.9f * (1f - k));
                yield return null;
            }
            Destroy(go);
            Destroy(m);
        }

        // Let the crack through, then drop every sound and bring it back slowly.
        IEnumerator Duck()
        {
            yield return new WaitForSeconds(0.06f);
            float t = 0f;
            const float hold = 0.3f, rise = 0.9f;
            while (t < hold + rise)
            {
                t += Time.deltaTime;
                float k = t < hold ? 0f : Mathf.Clamp01((t - hold) / rise);
                AudioListener.volume = ShrimpSettings.Volume * Mathf.Lerp(0.2f, 1f, k);
                yield return null;
            }
            AudioListener.volume = ShrimpSettings.Volume;
            _duck = null;
        }

        void Update()
        {
            if (_head == null && Camera.main != null) _head = Camera.main.transform;
            if (Menu != null) Menu.Tick();
            ApplyBrightness();

            if (Playing && !Over)
            {
                PlayTime += Time.deltaTime;
                Food -= maxFood / Mathf.Max(1f, secondsToStarve) * ShrimpSettings.HungerScale * Time.deltaTime;
                if (Food <= 0f)
                {
                    Food = 0f;
                    EndStarved();
                }
                else if (surviveSeconds > 0f && PlayTime >= surviveSeconds)
                {
                    End("YOU SURVIVED", FactGoby, false);
                }
                else if (Food / maxFood < hungryBelow)
                {
                    // Two quick beats, then a pause.
                    _heartbeat -= Time.deltaTime;
                    if (_heartbeat <= 0f)
                    {
                        _heartbeat = 1.3f;
                        ShrimpSettings.Haptic(true, 0.5f, 0.07f);
                        ShrimpSettings.Haptic(false, 0.5f, 0.07f);
                    }
                }
            }
            UpdateBar();
        }

        // ------------------------------------------------------------------ food bar
        void UpdateBar()
        {
            if (_head == null) return;
            if (_bar == null) BuildBar();
            if (_bar.parent != _head)
            {
                _bar.SetParent(_head, false);
                _bar.localPosition = new Vector3(0f, -0.2f, 0.62f);
                _bar.localRotation = Quaternion.Euler(18f, 0f, 0f);
            }
            float k = Mathf.Clamp01(Food / Mathf.Max(1f, maxFood));
            _fill.localScale = new Vector3(Mathf.Max(0.0001f, k), 1f, 1f);
            Color c = k < hungryBelow
                ? Color.Lerp(new Color(0.9f, 0.15f, 0.1f), new Color(0.5f, 0.05f, 0.05f), Mathf.PingPong(Time.time * 2f, 1f))
                : Color.Lerp(new Color(0.95f, 0.75f, 0.2f), new Color(0.35f, 0.9f, 0.45f), k);
            _fillRenderer.material.color = c;
            _label.text = Food <= 0f ? "STARVED" : "FOOD";

            // Session clock, top left of the clear part of the view.
            if (_timer == null)
            {
                _timer = ShrimpUI.Label("Timer", _head, "0:00", 0.02f, Color.white);
                _timer.transform.localPosition = new Vector3(-0.16f, 0.125f, 0.62f);
                _timer.transform.localRotation = Quaternion.Euler(-11f, -14f, 0f);
            }
            int seconds = Mathf.FloorToInt(PlayTime);
            _timer.text = (seconds / 60) + ":" + (seconds % 60).ToString("00");
        }

        void BuildBar()
        {
            const float width = 0.24f, height = 0.02f;
            GameObject root = new GameObject("FoodBar");
            _bar = root.transform;

            Transform back = ShrimpUI.Quad("Back", _bar, new Color(0.03f, 0.05f, 0.07f, 0.85f));
            back.localScale = new Vector3(width + 0.012f, height + 0.012f, 1f);
            back.localPosition = new Vector3(0f, 0f, 0.002f);

            // The fill hangs off a pivot at the bar's left end, so scaling the pivot drains it to the left.
            GameObject pivot = new GameObject("FillPivot");
            pivot.transform.SetParent(_bar, false);
            pivot.transform.localPosition = new Vector3(-width * 0.5f, 0f, 0f);
            _fill = pivot.transform;
            Transform fillQuad = ShrimpUI.Quad("Fill", _fill, Color.white);
            fillQuad.localScale = new Vector3(width, height, 1f);
            fillQuad.localPosition = new Vector3(width * 0.5f, 0f, 0f);
            _fillRenderer = fillQuad.GetComponent<Renderer>();

            _label = ShrimpUI.Label("Label", _bar, "FOOD", 0.012f, Color.white);
            _label.transform.localPosition = new Vector3(0f, 0.026f, 0f);
        }

        // ------------------------------------------------------------------ brightness
        void ApplyBrightness()
        {
            if (!_brightnessCached)
            {
                _sun = RenderSettings.sun;
                if (_sun == null)
                {
                    Light any = Object.FindAnyObjectByType<Light>();
                    if (any != null && any.type == LightType.Directional) _sun = any;
                }
                if (_sun != null) _sunBase = _sun.intensity;
                _ambientBase = RenderSettings.ambientIntensity;
                _ambientColorBase = RenderSettings.ambientLight;
                _fogBase = RenderSettings.fogDensity;
                _sense = Object.FindAnyObjectByType<VibrationSense>();
                if (_sense != null) _murkBase = _sense.murkDensity;
                _brightnessCached = true;
            }

            // Difficulty changes how far you can see. Whatever fog the scene was saved with counts as Normal.
            if (_sense != null && _sense.isActiveAndEnabled && _sense.murk)
            {
                _sense.murkDensity = _murkBase * ShrimpSettings.MurkScale;
            }
            else
            {
                RenderSettings.fogDensity = _fogBase * ShrimpSettings.MurkScale;
            }
            float b = ShrimpSettings.Brightness;
            if (_sun != null) _sun.intensity = _sunBase * b;
            RenderSettings.ambientIntensity = _ambientBase * b;
            RenderSettings.ambientLight = _ambientColorBase * b;
        }
    }

    /// <summary>Small helpers for flat, unlit UI pieces that need no assets.</summary>
    public static class ShrimpUI
    {
        static Font _font;
        static Shader _shader;

        public static Material FlatMaterial(Color color)
        {
            if (_shader == null) _shader = Shader.Find("Sprites/Default");
            Material m = new Material(_shader);
            m.color = color;
            return m;
        }

        public static Transform Quad(string name, Transform parent, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            Collider col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            go.transform.SetParent(parent, false);
            Renderer r = go.GetComponent<Renderer>();
            r.sharedMaterial = FlatMaterial(color);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go.transform;
        }

        /// <summary>Width of the widest line of a label, in the label's own units.</summary>
        public static float TextWidth(TextMesh tm)
        {
            Font font = tm.font;
            string text = tm.text;
            if (font == null || string.IsNullOrEmpty(text)) return 0f;
            font.RequestCharactersInTexture(text, tm.fontSize, tm.fontStyle);
            float widest = 0f, line = 0f;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n') { widest = Mathf.Max(widest, line); line = 0f; continue; }
                CharacterInfo info;
                if (font.GetCharacterInfo(text[i], out info, tm.fontSize, tm.fontStyle)) line += info.advance;
            }
            widest = Mathf.Max(widest, line);
            return widest * tm.characterSize * 0.1f;
        }

        /// <summary>Sets a label to its normal size, then shrinks it if the text is wider than maxWidth.</summary>
        public static void Fit(TextMesh tm, float size, float maxWidth)
        {
            tm.characterSize = size * 0.1f;
            float width = TextWidth(tm);
            if (width > maxWidth && width > 0f) tm.characterSize *= maxWidth / width;
        }

        static Font _titleFont;

        /// <summary>The title font: ShrimpTitleFont.ttf in a Resources folder, or the menu font if it is missing.</summary>
        public static Font TitleFont()
        {
            if (_titleFont == null) _titleFont = Resources.Load<Font>("ShrimpTitleFont");
            return _titleFont;
        }

        public static TextMesh Label(string name, Transform parent, string text, float size, Color color)
        {
            return Label(name, parent, text, size, color, null);
        }

        public static TextMesh Label(string name, Transform parent, string text, float size, Color color, Font font)
        {
            // ShrimpFont.ttf in a Resources folder is the game's display font (Luckiest Guy, Apache 2.0).
            if (_font == null) _font = Resources.Load<Font>("ShrimpFont");
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            if (font == null) font = _font;
            TextMesh tm = go.AddComponent<TextMesh>();
            tm.font = font;
            tm.fontSize = 72;
            tm.characterSize = size * 0.1f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            tm.text = text;
            MeshRenderer r = go.GetComponent<MeshRenderer>();
            if (font != null) r.sharedMaterial = font.material;
            return tm;
        }
    }
}
