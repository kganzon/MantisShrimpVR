using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PistolShrimp
{
    /// <summary>
    /// The main menu, built in code and floating in front of the player.
    /// There is no pointer: you choose a button by shooting it with the claw.
    /// Main (Play, Customize, Options), Customize (three skins), Options (audio, vibration, brightness).
    /// Reopen it in the game with the left controller's menu button, or M on a keyboard.
    /// </summary>
    public class ShrimpMenu : MonoBehaviour
    {
        static readonly string[] SkinNames = { "CANDY STRIPE", "TIGER ORANGE", "GLASS RED BAND" };
        static readonly Color ButtonColor = new Color(0.07f, 0.33f, 0.42f, 0.95f);
        static readonly Color HoverColor = new Color(0.95f, 0.55f, 0.15f, 1f);
        static readonly Color PanelColor = new Color(0.02f, 0.08f, 0.11f, 0.82f);

        public bool IsOpen { get { return _open; } }

        ShrimpGame _game;
        Transform _head;
        bool _open;
        GameObject _main, _customize, _options;
        TextMesh _title, _hint, _playLabel, _audioLabel, _vibrationLabel, _brightnessLabel, _difficultyLabel, _handLabel, _musicLabel;
        readonly List<ShrimpMenuButton> _buttons = new List<ShrimpMenuButton>();
        readonly List<TextMesh> _skinLabels = new List<TextMesh>();
        LineRenderer _aim;
        InputAction _menuAction;
        float _ignoreUntil;
        float _placeUntil;
        GameObject _ending;
        TextMesh _endBody;
        GameObject _dish;
        GameObject _restartButton;

        // Every label remembers its normal size and the room it has, so long text shrinks to fit.
        class FitEntry { public TextMesh label; public float size; public float maxWidth; public string last; }
        readonly List<FitEntry> _fits = new List<FitEntry>();

        TextMesh Fitted(TextMesh label, float size, float maxWidth)
        {
            FitEntry e = new FitEntry();
            e.label = label; e.size = size; e.maxWidth = maxWidth; e.last = null;
            _fits.Add(e);
            return label;
        }

        void FitLabels()
        {
            for (int i = 0; i < _fits.Count; i++)
            {
                FitEntry e = _fits[i];
                if (e.label == null || e.label.text == e.last) continue;
                e.last = e.label.text;
                ShrimpUI.Fit(e.label, e.size, e.maxWidth);
            }
        }
        bool _caught;
        float _endTime;

        static readonly Color CaughtRed = new Color(1f, 0.12f, 0.08f);
        // The pink-orange of a cooked shrimp.
        static readonly Color CookedShrimp = new Color(0.98f, 0.50f, 0.35f);
        const float TitleSize = 0.125f;

        public void Init(ShrimpGame game)
        {
            _game = game;
            Build();
            _menuAction = new InputAction("menu", InputActionType.Button, "<XRController>{LeftHand}/menu");
            _menuAction.AddBinding("<XRController>{LeftHand}/menuButton");
            _menuAction.AddBinding("<XRController>{LeftHand}/secondaryButton");    // Y
            _menuAction.AddBinding("<XRController>{RightHand}/secondaryButton");   // B
            _menuAction.AddBinding("<Keyboard>/m");
            _menuAction.Enable();
        }

        void OnDestroy()
        {
            if (_menuAction != null)
            {
                _menuAction.Disable();
                _menuAction.Dispose();
            }
        }

        // ------------------------------------------------------------------ open / close
        public void Show()
        {
            Open(_main);
            _caught = false;
            _title.color = CookedShrimp;
            _title.transform.localScale = Vector3.one;
            _title.text = "PISTOL SHRIMP\nSIMULATOR";
            bool midGame = _game != null && _game.PlayTime > 0f;
            _playLabel.text = midGame ? "RESUME" : "PLAY";
            _restartButton.SetActive(midGame);      // only offered once a run is under way
        }

        /// <summary>The end screen: what happened, how the run went, and one fact about the real animal.</summary>
        public void ShowEnding(string title, string body, bool caught)
        {
            Open(_ending);
            _caught = caught;
            _endTime = 0f;
            _title.text = title;
            _title.color = caught ? CaughtRed : CookedShrimp;
            _endBody.text = body;
            _endBody.color = caught ? new Color(1f, 0.55f, 0.45f) : new Color(0.85f, 0.95f, 1f);
            // With the bowl on screen the text moves down to make room.
            _endBody.transform.localPosition = new Vector3(0f, caught ? -0.19f : 0.06f, 0f);
            if (_dish != null) Destroy(_dish);
            if (caught)
            {
                _dish = new GameObject("FriedRice");
                _dish.transform.SetParent(_ending.transform, false);
                _dish.transform.localPosition = new Vector3(0f, 0.1f, -0.22f);
                _dish.transform.localScale = Vector3.one * 0.62f;
                _dish.AddComponent<FriedRiceEnding>();
            }
            ShowOnlyThisCard(caught);
        }

        const int CardLayer = 31;

        // The run is over: the reef, the animals, the claws and the HUD all disappear and only
        // the end card is left on a dark background. The card is moved to a layer of its own and
        // the headset camera is told to draw nothing else. The scene reloads on Play Again,
        // which puts everything back.
        void ShowOnlyThisCard(bool caught)
        {
            Transform[] all = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++) all[i].gameObject.layer = CardLayer;

            // Vibration Sense repaints the camera background every frame, so switch it off first.
            VibrationSense sense = FindAnyObjectByType<VibrationSense>();
            if (sense != null) sense.enabled = false;

            Camera cam = _head != null ? _head.GetComponent<Camera>() : Camera.main;
            if (cam == null) return;
            cam.cullingMask = 1 << CardLayer;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = caught ? new Color(0.10f, 0.01f, 0.01f, 1f) : new Color(0.01f, 0.04f, 0.06f, 1f);
            RenderSettings.fog = false;       // the bowl should not fade into reef fog that is no longer there
        }

        void Open(GameObject page)
        {
            if (_head == null && Camera.main != null) _head = Camera.main.transform;
            if (_game != null) _game.Pause();
            _open = true;
            gameObject.SetActive(true);
            ShowPage(page);
            Place();
            _ignoreUntil = Time.unscaledTime + 0.6f;   // a shot already in the water must not pick a button
            _placeUntil = Time.unscaledTime + 1f;      // keep it in front while head tracking settles
        }

        public void Hide()
        {
            _open = false;
            gameObject.SetActive(false);
            if (_aim != null) _aim.enabled = false;
        }

        void Place()
        {
            if (_head == null) return;
            Vector3 forward = _head.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            forward.Normalize();
            float s = Mathf.Max(0.0001f, _head.lossyScale.x);
            transform.localScale = Vector3.one * s;
            transform.position = _head.position + (forward * 1.25f + Vector3.down * 0.16f) * s;
            transform.rotation = Quaternion.LookRotation(forward);
        }

        void ShowPage(GameObject page)
        {
            _main.SetActive(page == _main);
            _customize.SetActive(page == _customize);
            _options.SetActive(page == _options);
            _ending.SetActive(page == _ending);
            _hint.gameObject.SetActive(page != _ending);
            Refresh();
        }

        // ------------------------------------------------------------------ per frame
        void Update()
        {
            if (!_open) return;
            if (_head == null && Camera.main != null) _head = Camera.main.transform;
            if (Time.unscaledTime < _placeUntil) Place();
            FitLabels();
            if (_caught)
            {
                // The verdict slams in, then keeps a slow pulse.
                _endTime += Time.deltaTime;
                float slam = Mathf.Lerp(2.4f, 1f, Mathf.Clamp01(_endTime / 0.25f));
                float pulse = 1f + 0.05f * Mathf.Sin(_endTime * 3.2f);
                _title.transform.localScale = Vector3.one * slam * pulse;
            }

            // Light up the button the claw points at, and draw a faint aiming line.
            Transform muzzle = _game != null && _game.Player != null ? _game.Player.muzzle : null;
            ShrimpMenuButton hovered = null;
            if (muzzle != null)
            {
                Ray ray = new Ray(muzzle.position, muzzle.forward);
                float best = 6f;
                for (int i = 0; i < _buttons.Count; i++)
                {
                    ShrimpMenuButton b = _buttons[i];
                    if (b == null || !b.gameObject.activeInHierarchy) continue;
                    RaycastHit hit;
                    if (b.Box.Raycast(ray, out hit, best))
                    {
                        best = hit.distance;
                        hovered = b;
                    }
                }
                if (_aim != null)
                {
                    _aim.enabled = true;
                    _aim.SetPosition(0, muzzle.position);
                    _aim.SetPosition(1, muzzle.position + muzzle.forward * Mathf.Min(best, 3f));
                }
            }
            for (int i = 0; i < _buttons.Count; i++)
            {
                if (_buttons[i] != null) _buttons[i].SetHover(_buttons[i] == hovered);
            }
        }

        // Runs even while the menu object is hidden, because ShrimpGame calls it.
        public void Tick()
        {
            if (_menuAction != null && _menuAction.WasPressedThisFrame())
            {
                if (_open && _game != null && !_game.Over) { Hide(); _game.StartPlaying(); }
                else if (!_open) Show();
            }
        }

        public void Press(ShrimpMenuButton button)
        {
            if (!_open || Time.unscaledTime < _ignoreUntil) return;
            _ignoreUntil = Time.unscaledTime + 0.25f;
            ShrimpSettings.Haptic(true, 0.6f, 0.05f);
            ShrimpSettings.Haptic(false, 0.6f, 0.05f);
            if (button.Action != null) button.Action();
            Refresh();
        }

        void Refresh()
        {
            _audioLabel.text = "AUDIO  " + Mathf.RoundToInt(ShrimpSettings.Volume * 100f) + "%";
            _musicLabel.text = "MUSIC  " + Mathf.RoundToInt(ShrimpSettings.MusicVolume * 100f) + "%";
            _vibrationLabel.text = ShrimpSettings.Vibration ? "VIBRATION  ON" : "VIBRATION  OFF";
            _brightnessLabel.text = "BRIGHTNESS  " + Mathf.RoundToInt(ShrimpSettings.Brightness * 100f) + "%";
            _difficultyLabel.text = "DIFFICULTY  " + ShrimpSettings.DifficultyNames[ShrimpSettings.Difficulty];
            _handLabel.text = ShrimpSettings.LeftHanded ? "LEFT HANDED" : "RIGHT HANDED";
            for (int i = 0; i < _skinLabels.Count; i++)
            {
                _skinLabels[i].text = i == ShrimpSettings.Skin ? "> " + SkinNames[i] + " <" : SkinNames[i];
            }
        }

        // ------------------------------------------------------------------ building
        void Build()
        {
            Transform panel = ShrimpUI.Quad("Panel", transform, PanelColor);
            panel.localScale = new Vector3(1.1f, 1.2f, 1f);
            panel.localPosition = new Vector3(0f, 0.115f, 0.03f);

            _title = Fitted(ShrimpUI.Label("Title", transform, "PISTOL SHRIMP\nSIMULATOR", TitleSize, CookedShrimp, ShrimpUI.TitleFont()), TitleSize, 1.02f);
            _title.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            _hint = ShrimpUI.Label("Hint", transform, "Squeeze the trigger to cock the claw. Let go to fire at a button.\nPause any time with Y, B or the menu button.", 0.028f, new Color(0.75f, 0.9f, 0.95f));
            _hint.transform.localPosition = new Vector3(0f, -0.44f, 0f);
            Fitted(_hint, 0.028f, 1.04f);

            _main = Page("Main");
            _playLabel = Button(_main, "PLAY", 0f, 0.21f, 0.6f, delegate { Hide(); if (_game != null) _game.StartPlaying(); });
            Button(_main, "CUSTOMIZE", 0f, 0.07f, 0.6f, delegate { ShowPage(_customize); });
            Button(_main, "OPTIONS", 0f, -0.07f, 0.6f, delegate { ShowPage(_options); });
            TextMesh restart = Button(_main, "RESTART", 0f, -0.21f, 0.6f, delegate { Hide(); if (_game != null) _game.Restart(); });
            _restartButton = restart.transform.parent.gameObject;

            _customize = Page("Customize");
            for (int i = 0; i < SkinNames.Length; i++)
            {
                int index = i;
                _skinLabels.Add(Button(_customize, SkinNames[i], 0f, 0.2f - i * 0.15f, 0.7f, delegate { SetSkin(index); }));
            }
            Button(_customize, "BACK", 0f, -0.27f, 0.4f, delegate { ShowPage(_main); });

            _options = Page("Options");
            // Rows, top to bottom, 0.12 apart.
            _audioLabel = RowLabel(_options, 0.27f);
            Button(_options, "-", -0.44f, 0.27f, 0.13f, delegate { ShrimpSettings.Volume = Step(ShrimpSettings.Volume, -0.1f, 0f, 1f); ShrimpSettings.Save(); });
            Button(_options, "+", 0.44f, 0.27f, 0.13f, delegate { ShrimpSettings.Volume = Step(ShrimpSettings.Volume, 0.1f, 0f, 1f); ShrimpSettings.Save(); });
            _musicLabel = RowLabel(_options, 0.15f);
            Button(_options, "-", -0.44f, 0.15f, 0.13f, delegate { ShrimpSettings.MusicVolume = Step(ShrimpSettings.MusicVolume, -0.1f, 0f, 1f); ShrimpSettings.Save(); });
            Button(_options, "+", 0.44f, 0.15f, 0.13f, delegate { ShrimpSettings.MusicVolume = Step(ShrimpSettings.MusicVolume, 0.1f, 0f, 1f); ShrimpSettings.Save(); });
            _vibrationLabel = Button(_options, "VIBRATION  ON", -0.27f, 0.03f, 0.5f, delegate { ShrimpSettings.Vibration = !ShrimpSettings.Vibration; ShrimpSettings.Save(); });
            // Swaps the big snapper claw to the other hand.
            _handLabel = Button(_options, "RIGHT HANDED", 0.27f, 0.03f, 0.5f, delegate
            {
                ShrimpSettings.LeftHanded = !ShrimpSettings.LeftHanded;
                ShrimpSettings.Save();
                if (_game != null) _game.ApplyHandedness();
            });
            _brightnessLabel = RowLabel(_options, -0.09f);
            Button(_options, "-", -0.44f, -0.09f, 0.13f, delegate { ShrimpSettings.Brightness = Step(ShrimpSettings.Brightness, -0.1f, 0.5f, 1.5f); ShrimpSettings.Save(); });
            Button(_options, "+", 0.44f, -0.09f, 0.13f, delegate { ShrimpSettings.Brightness = Step(ShrimpSettings.Brightness, 0.1f, 0.5f, 1.5f); ShrimpSettings.Save(); });
            // One button that cycles easy, normal, hard.
            _difficultyLabel = Button(_options, "DIFFICULTY  EASY", 0f, -0.21f, 0.7f, delegate { ShrimpSettings.Difficulty = (ShrimpSettings.Difficulty + 1) % 3; ShrimpSettings.Save(); });
            Button(_options, "BACK", 0f, -0.33f, 0.4f, delegate { ShowPage(_main); });

            _ending = Page("Ending");
            _endBody = ShrimpUI.Label("Body", _ending.transform, "", 0.03f, new Color(0.85f, 0.95f, 1f));
            _endBody.transform.localPosition = new Vector3(0f, 0.06f, 0f);
            Fitted(_endBody, 0.03f, 1.04f);
            Button(_ending, "PLAY AGAIN", 0f, -0.33f, 0.6f, delegate { Hide(); if (_game != null) _game.StartPlaying(); });

            GameObject line = new GameObject("AimLine");
            line.transform.SetParent(transform, false);
            _aim = line.AddComponent<LineRenderer>();
            _aim.useWorldSpace = true;
            _aim.positionCount = 2;
            _aim.startWidth = 0.004f;
            _aim.endWidth = 0.002f;
            _aim.sharedMaterial = ShrimpUI.FlatMaterial(new Color(1f, 0.85f, 0.4f, 0.7f));
            _aim.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _aim.enabled = false;
        }

        static float Step(float value, float delta, float min, float max)
        {
            return Mathf.Clamp(Mathf.Round((value + delta) * 10f) / 10f, min, max);
        }

        void SetSkin(int index)
        {
            ShrimpSettings.Skin = index;
            ShrimpSettings.Save();
            if (_game != null && _game.Player != null) _game.Player.SetColorway(index);
        }

        GameObject Page(string name)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go;
        }

        TextMesh RowLabel(GameObject page, float y)
        {
            TextMesh t = Fitted(ShrimpUI.Label("RowLabel", page.transform, "", 0.042f, Color.white), 0.042f, 0.7f);
            t.transform.localPosition = new Vector3(0f, y, 0f);
            return t;
        }

        TextMesh Button(GameObject page, string text, float x, float y, float width, Action action)
        {
            const float height = 0.11f;
            GameObject go = new GameObject("Button " + text);
            go.transform.SetParent(page.transform, false);
            go.transform.localPosition = new Vector3(x, y, 0f);

            BoxCollider box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(width, height, 0.06f);
            box.center = new Vector3(0f, 0f, 0.03f);

            Transform face = ShrimpUI.Quad("Face", go.transform, ButtonColor);
            face.localScale = new Vector3(width, height, 1f);
            face.localPosition = new Vector3(0f, 0f, 0.01f);

            TextMesh label = Fitted(ShrimpUI.Label("Label", go.transform, text, 0.042f, Color.white), 0.042f, width - 0.04f);
            label.transform.localPosition = new Vector3(0f, 0f, -0.005f);

            ShrimpMenuButton button = go.AddComponent<ShrimpMenuButton>();
            button.Setup(this, box, face.GetComponent<Renderer>(), ButtonColor, HoverColor, action);
            _buttons.Add(button);
            return label;
        }
    }

    /// <summary>A menu button. It is pressed by a direct hit from the water jet.</summary>
    public class ShrimpMenuButton : MonoBehaviour, ISnapHittable
    {
        public Action Action { get; private set; }
        public BoxCollider Box { get; private set; }

        ShrimpMenu _menu;
        Renderer _face;
        Color _normal, _hover;
        bool _hovered;

        public void Setup(ShrimpMenu menu, BoxCollider box, Renderer face, Color normal, Color hover, Action action)
        {
            _menu = menu;
            Box = box;
            _face = face;
            _normal = normal;
            _hover = hover;
            Action = action;
        }

        public void SetHover(bool on)
        {
            if (on == _hovered || _face == null) return;
            _hovered = on;
            _face.material.color = on ? _hover : _normal;
        }

        public void OnSnapHit(SnapHit hit)
        {
            // Only the button the jet actually touched reacts, not its neighbours inside the shockwave.
            if (hit.direct && _menu != null) _menu.Press(this);
        }
    }
}
