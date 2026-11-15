using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace PistolShrimp
{
    public enum SnapperHand { Right, Left }

    public enum SnapMode
    {
        /// <summary>Squeeze the trigger to cock the claw open, let go to snap. Closest to the real animal.</summary>
        CockAndRelease,
        /// <summary>Claw rests cocked open. Pull the trigger to snap. Feels like a normal VR gun.</summary>
        SnapOnPull
    }

    public enum ClawPose
    {
        /// <summary>Aim pose (pointer). The claw points where the controller points. Falls back to Grip if the device has no aim pose.</summary>
        Aim,
        /// <summary>Grip pose. The claw follows the palm.</summary>
        Grip
    }

    [System.Serializable]
    public class ClawArm
    {
        public Transform upper;
        public Transform lower;
        public Transform claw;
        public Transform dactyl;
        [Tooltip("Where the claw's wrist sits relative to the controller pose, in metres. Negative Z pulls the claw back over the hand.")]
        public Vector3 positionOffset;
        [Tooltip("Extra rotation of the claw relative to the controller pose, in degrees.")]
        public Vector3 rotationOffset;

        [System.NonSerialized] public Quaternion upperRest;
        [System.NonSerialized] public Quaternion lowerRest;
        [System.NonSerialized] public Quaternion clawRestLocal;
        [System.NonSerialized] public Vector3 clawRestLocalPos;
        [System.NonSerialized] public Quaternion dactylRest;
        [System.NonSerialized] public Quaternion clawRestInModel;
        [System.NonSerialized] public Vector3 hingeAxisLocal;
        [System.NonSerialized] public float openAngle;
        [System.NonSerialized] public bool tracked;

        public bool IsValid
        {
            get { return upper != null && lower != null && claw != null && dactyl != null; }
        }
    }

    /// <summary>
    /// First-person pistol shrimp for VR.
    ///  - The body hangs from the headset: the player's eyes are the shrimp's eyes.
    ///  - Each claw is pinned to a controller. The arm segments are solved with two-bone IK.
    ///  - The big claw cocks open and snaps shut to fire a WaterBullet from the muzzle.
    ///  - The small claw is a pincer that can pick up rigidbodies.
    /// Needs only the Input System package (XR Interaction Toolkit already depends on it).
    /// Drop the prefab in a scene that has an XR Origin. Nothing has to be wired by hand.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public class PistolShrimpPlayer : MonoBehaviour
    {
        [Header("Rig (filled in by Tools > Pistol Shrimp > Build Prefabs)")]
        public SnapperHand snapperHand = SnapperHand.Right;
        public ClawArm snap = new ClawArm();
        public ClawArm pinch = new ClawArm();
        public Transform eyeAnchor;
        public Transform muzzle;
        public Transform pinchTip;
        public SkinnedMeshRenderer bodyRenderer;

        [Header("Tracking (leave empty to auto-detect)")]
        [Tooltip("The headset camera. Empty = Camera.main.")]
        public Transform head;
        [Tooltip("Parent that controller poses are relative to (XR Origin > Camera Offset). Empty = the head's parent.")]
        public Transform trackingSpace;
        [Tooltip("Optional. If set, the claw follows this transform instead of reading the controller pose from the Input System.")]
        public Transform rightControllerOverride;
        public Transform leftControllerOverride;
        public ClawPose clawPose = ClawPose.Aim;

        [Header("Body")]
        public bool followHead = true;
        [Tooltip("Where the shrimp's eye point sits relative to the headset, in metres (head yaw space).")]
        public Vector3 eyeOffset = new Vector3(0f, 0f, 0f);
        [Tooltip("Seconds for the body to catch up with a head turn. 0 = locked to the head.")]
        public float bodyTurnSmoothTime = 0.12f;
        [Tooltip("Copy the XR rig's scale so the shrimp stays hand-sized if you scale the rig to make the world feel big.")]
        public bool matchRigScale = true;

        [Header("Snapper claw")]
        public SnapMode snapMode = SnapMode.CockAndRelease;
        public float snapOpenAngle = 78f;
        [Tooltip("Seconds of trigger squeeze to cock the claw fully open.")]
        public float cockTime = 0.22f;
        [Tooltip("Seconds for the hammer to slam shut. The real animal does it in under a millisecond.")]
        public float snapCloseTime = 0.03f;
        public float cooldown = 0.3f;
        [Range(0.05f, 0.95f)] public float releaseThreshold = 0.35f;
        [Range(0.05f, 0.95f)] public float pullThreshold = 0.6f;
        public WaterBullet bulletPrefab;
        public AudioSource snapAudio;
        public AudioClip snapClip;

        [Header("Pincer claw")]
        public float pinchOpenAngle = 38f;
        public float grabRadius = 0.07f;
        public LayerMask grabMask = ~0;

        [Header("Feel")]
        public bool haptics = true;
        [Tooltip("Lets you test without a headset: Space = snapper trigger, Left Shift = pincer.")]
        public bool keyboardFallback = true;

        [Header("Look")]
        public Material[] colorways;
        public int colorway;

        [Header("Events")]
        public UnityEvent onCocked;
        public UnityEvent onSnap;

        /// <summary>0 = claw shut, 1 = fully cocked.</summary>
        public float CockAmount { get { return _cock; } }
        public bool IsCocked { get { return _locked; } }
        public Rigidbody HeldBody { get { return _held; } }

        class HandInput
        {
            public InputAction aimPos, aimRot, gripPos, gripRot, tracked, trigger, grip;
        }

        HandInput _right;
        HandInput _left;
        bool _restCached;
        float _bodyYaw;
        float _yawVelocity;
        bool _yawInitialised;
        float _cock;
        bool _locked;
        float _cooldownLeft;
        float _hapticTimer;
        bool _triggerWasDown;
        bool _pinchClosed;
        float _pinchAmount;
        Rigidbody _held;
        bool _heldWasKinematic;
        Vector3 _heldLocalPos;
        Quaternion _heldLocalRot;

        bool SnapIsRight { get { return snapperHand == SnapperHand.Right; } }

        float WorldScale { get { return Mathf.Max(0.0001f, transform.lossyScale.x); } }

        // ------------------------------------------------------------------ lifecycle
        void Awake()
        {
            CacheRest();
            ApplyColorway();
        }

        void OnEnable()
        {
            _right = MakeHand("{RightHand}", SnapIsRight);
            _left = MakeHand("{LeftHand}", !SnapIsRight);
            Application.onBeforeRender += OnBeforeRender;
        }

        void OnDisable()
        {
            Application.onBeforeRender -= OnBeforeRender;
            DisposeHand(_right);
            DisposeHand(_left);
            _right = null;
            _left = null;
            ReleaseHeld();
        }

        void OnValidate()
        {
            ApplyColorway();
        }

        void CacheRest()
        {
            if (_restCached) return;
            CacheArm(snap);
            CacheArm(pinch);
            _restCached = true;
        }

        void CacheArm(ClawArm a)
        {
            if (!a.IsValid) return;
            a.upperRest = a.upper.localRotation;
            a.lowerRest = a.lower.localRotation;
            a.clawRestLocal = a.claw.localRotation;
            a.clawRestLocalPos = a.claw.localPosition;
            a.dactylRest = a.dactyl.localRotation;
            // Everything below is measured against this object's axes, so it does not
            // matter how the FBX importer oriented the individual bones.
            a.clawRestInModel = Quaternion.Inverse(transform.rotation) * a.claw.rotation;
            a.hingeAxisLocal = a.dactyl.parent.InverseTransformDirection(transform.right);
        }

        // ------------------------------------------------------------------ input
        HandInput MakeHand(string usage, bool isSnapper)
        {
            string dev = "<XRController>" + usage + "/";
            HandInput h = new HandInput();
            h.aimPos = new InputAction("aimPos", InputActionType.Value, dev + "pointerPosition", null, null, "Vector3");
            h.aimRot = new InputAction("aimRot", InputActionType.Value, dev + "pointerRotation", null, null, "Quaternion");
            h.gripPos = new InputAction("gripPos", InputActionType.Value, dev + "devicePosition", null, null, "Vector3");
            h.gripRot = new InputAction("gripRot", InputActionType.Value, dev + "deviceRotation", null, null, "Quaternion");
            h.tracked = new InputAction("tracked", InputActionType.Value, dev + "isTracked");
            h.trigger = new InputAction("trigger", InputActionType.Value, dev + "trigger");
            h.grip = new InputAction("grip", InputActionType.Value, dev + "grip");
            if (keyboardFallback)
            {
                h.trigger.AddBinding(isSnapper ? "<Keyboard>/space" : "<Keyboard>/leftShift");
            }
            h.aimPos.Enable(); h.aimRot.Enable(); h.gripPos.Enable(); h.gripRot.Enable();
            h.tracked.Enable(); h.trigger.Enable(); h.grip.Enable();
            return h;
        }

        static void DisposeHand(HandInput h)
        {
            if (h == null) return;
            InputAction[] all = { h.aimPos, h.aimRot, h.gripPos, h.gripRot, h.tracked, h.trigger, h.grip };
            for (int i = 0; i < all.Length; i++)
            {
                all[i].Disable();
                all[i].Dispose();
            }
        }

        HandInput Hand(bool right)
        {
            return right ? _right : _left;
        }

        bool TryGetControllerPose(bool right, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;

            Transform over = right ? rightControllerOverride : leftControllerOverride;
            if (over != null)
            {
                position = over.position;
                rotation = over.rotation;
                return over.gameObject.activeInHierarchy;
            }

            HandInput h = Hand(right);
            if (h == null) return false;
            InputAction pos = h.aimPos;
            InputAction rot = h.aimRot;
            if (clawPose == ClawPose.Grip || pos.controls.Count == 0 || rot.controls.Count == 0)
            {
                pos = h.gripPos;
                rot = h.gripRot;
            }
            if (pos.controls.Count == 0 || rot.controls.Count == 0) return false;
            if (h.tracked.controls.Count > 0 && h.tracked.ReadValue<float>() < 0.5f) return false;

            Vector3 lp = pos.ReadValue<Vector3>();
            Quaternion lr = rot.ReadValue<Quaternion>();
            if (lr.x == 0f && lr.y == 0f && lr.z == 0f && lr.w == 0f) return false;

            Transform space = trackingSpace;
            if (space != null)
            {
                position = space.TransformPoint(lp);
                rotation = space.rotation * lr;
            }
            else
            {
                position = lp;
                rotation = lr;
            }
            return true;
        }

        float ReadTrigger(bool right)
        {
            HandInput h = Hand(right);
            return h == null ? 0f : Mathf.Clamp01(h.trigger.ReadValue<float>());
        }

        float ReadGrip(bool right)
        {
            HandInput h = Hand(right);
            return h == null ? 0f : Mathf.Clamp01(h.grip.ReadValue<float>());
        }

        void Haptic(bool right, float amplitude, float duration)
        {
            if (!haptics) return;
            UnityEngine.XR.InputDevice device = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(
                right ? UnityEngine.XR.XRNode.RightHand : UnityEngine.XR.XRNode.LeftHand);
            if (device.isValid) device.SendHapticImpulse(0u, Mathf.Clamp01(amplitude), duration);
        }

        // ------------------------------------------------------------------ update
        void LateUpdate()
        {
            float dt = Time.deltaTime;
            ResolveTracking();
            UpdateBodyYaw(dt);
            UpdateSnapper(dt);
            UpdatePincer(dt);
            ApplyPose();
        }

        // Runs again right before rendering so the body and claws use the freshest
        // headset and controller poses. Without this, geometry close to the eyes
        // (rostrum, antennae) trails the head by a frame and visibly shakes.
        [BeforeRenderOrder(1000)]
        void OnBeforeRender()
        {
            if (isActiveAndEnabled && _restCached) ApplyPose();
        }

        void ResolveTracking()
        {
            if (head == null && Camera.main != null) head = Camera.main.transform;
            if (trackingSpace == null && head != null) trackingSpace = head.parent;
            if (matchRigScale && trackingSpace != null && transform.parent == null)
            {
                float s = trackingSpace.lossyScale.x;
                if (s > 0.0001f && Mathf.Abs(transform.localScale.x - s) > 0.0001f) transform.localScale = Vector3.one * s;
            }
        }

        void UpdateBodyYaw(float dt)
        {
            if (!followHead || head == null) return;
            float target = head.eulerAngles.y;
            if (!_yawInitialised || bodyTurnSmoothTime <= 0f)
            {
                _bodyYaw = target;
                _yawVelocity = 0f;
                _yawInitialised = true;
            }
            else
            {
                _bodyYaw = Mathf.SmoothDampAngle(_bodyYaw, target, ref _yawVelocity, bodyTurnSmoothTime, Mathf.Infinity, dt);
            }
        }

        void ApplyPose()
        {
            if (followHead && head != null && eyeAnchor != null)
            {
                Quaternion rot = Quaternion.Euler(0f, _bodyYaw, 0f);
                transform.rotation = rot;
                Vector3 want = head.position + rot * (eyeOffset * WorldScale);
                transform.position += want - eyeAnchor.position;
            }

            SolveArm(snap, SnapIsRight);
            SolveArm(pinch, !SnapIsRight);
            ApplyDactyl(snap, _cock * snapOpenAngle);
            ApplyDactyl(pinch, (1f - _pinchAmount) * pinchOpenAngle);

            if (_held != null && pinchTip != null)
            {
                _held.transform.SetPositionAndRotation(
                    pinchTip.TransformPoint(_heldLocalPos), pinchTip.rotation * _heldLocalRot);
            }
        }

        void SolveArm(ClawArm a, bool rightHand)
        {
            if (!a.IsValid) return;

            // Start from the rest pose every time. The IK below is then stable and the
            // Animator's idle clip cannot fight it.
            a.upper.localRotation = a.upperRest;
            a.lower.localRotation = a.lowerRest;
            a.claw.localRotation = a.clawRestLocal;
            a.claw.localPosition = a.clawRestLocalPos;

            Vector3 ctrlPos;
            Quaternion ctrlRot;
            a.tracked = TryGetControllerPose(rightHand, out ctrlPos, out ctrlRot);
            if (!a.tracked) return;   // no controller: the claw stays in its rest pose in front of the body

            Quaternion clawRot = ctrlRot * Quaternion.Euler(a.rotationOffset);
            Vector3 wrist = ctrlPos + clawRot * (a.positionOffset * WorldScale);

            TwoBoneIK(a.upper, a.lower, a.claw, wrist);

            // Pin the claw exactly to the hand, even if the arm cannot reach that far.
            a.claw.rotation = clawRot * a.clawRestInModel;
            a.claw.position = wrist;
        }

        static void ApplyDactyl(ClawArm a, float angle)
        {
            if (!a.IsValid) return;
            a.openAngle = angle;
            // Negative angle about the model's right axis lifts the hammer upward.
            a.dactyl.localRotation = Quaternion.AngleAxis(-angle, a.hingeAxisLocal) * a.dactylRest;
        }

        /// <summary>Analytic two-bone IK that only uses world positions, so bone axis conventions do not matter.</summary>
        static void TwoBoneIK(Transform upper, Transform lower, Transform end, Vector3 target)
        {
            Vector3 a = upper.position;
            Vector3 b = lower.position;
            Vector3 c = end.position;
            float lab = Vector3.Distance(a, b);
            float lcb = Vector3.Distance(b, c);
            if (lab < 0.00001f || lcb < 0.00001f) return;
            float lat = Mathf.Clamp(Vector3.Distance(a, target), 0.0001f, lab + lcb - 0.0001f);

            float acAb0 = Mathf.Acos(Mathf.Clamp(Vector3.Dot((c - a).normalized, (b - a).normalized), -1f, 1f));
            float baBc0 = Mathf.Acos(Mathf.Clamp(Vector3.Dot((a - b).normalized, (c - b).normalized), -1f, 1f));
            float acAb1 = Mathf.Acos(Mathf.Clamp((lcb * lcb - lab * lab - lat * lat) / (-2f * lab * lat), -1f, 1f));
            float baBc1 = Mathf.Acos(Mathf.Clamp((lat * lat - lab * lab - lcb * lcb) / (-2f * lab * lcb), -1f, 1f));

            Vector3 axis = Vector3.Cross(c - a, b - a);
            if (axis.sqrMagnitude < 0.0000001f) axis = Vector3.up;
            axis.Normalize();

            upper.rotation = Quaternion.AngleAxis((acAb1 - acAb0) * Mathf.Rad2Deg, axis) * upper.rotation;
            lower.rotation = Quaternion.AngleAxis((baBc1 - baBc0) * Mathf.Rad2Deg, axis) * lower.rotation;

            Vector3 reach = end.position - a;
            Vector3 want = target - a;
            if (reach.sqrMagnitude > 0.0000001f && want.sqrMagnitude > 0.0000001f)
            {
                upper.rotation = Quaternion.FromToRotation(reach, want) * upper.rotation;
            }
        }

        // ------------------------------------------------------------------ snapper
        void UpdateSnapper(float dt)
        {
            bool right = SnapIsRight;
            float trigger = ReadTrigger(right);

            if (_cooldownLeft > 0f)
            {
                _cooldownLeft -= dt;
                _cock = Mathf.MoveTowards(_cock, 0f, dt / Mathf.Max(0.001f, snapCloseTime));
                _triggerWasDown = trigger > pullThreshold;
                return;
            }

            if (snapMode == SnapMode.CockAndRelease)
            {
                if (_locked)
                {
                    _cock = 1f;
                    TensionBuzz(right, dt, 0.18f);
                    if (trigger < releaseThreshold) Snap();
                }
                else
                {
                    // A trigger rarely reports a clean 1.0, so 90% squeeze counts as fully cocked.
                    float want = Mathf.Clamp01(trigger / 0.9f);
                    float rate = want > _cock ? cockTime : cockTime * 0.5f;
                    _cock = Mathf.MoveTowards(_cock, want, dt / Mathf.Max(0.001f, rate));
                    if (trigger > 0.05f && want > _cock - 0.02f) TensionBuzz(right, dt, 0.05f + 0.2f * _cock);
                    if (_cock >= 0.98f) Lock(right);
                }
            }
            else
            {
                if (!_locked)
                {
                    _cock = Mathf.MoveTowards(_cock, 1f, dt / Mathf.Max(0.001f, cockTime));
                    if (_cock >= 0.98f) Lock(right);
                }
                else
                {
                    _cock = 1f;
                    bool down = trigger > pullThreshold;
                    if (down && !_triggerWasDown) Snap();
                }
            }
            _triggerWasDown = trigger > pullThreshold;
        }

        void Lock(bool right)
        {
            _locked = true;
            _cock = 1f;
            Haptic(right, 0.55f, 0.03f);
            if (onCocked != null) onCocked.Invoke();
        }

        void TensionBuzz(bool right, float dt, float amplitude)
        {
            _hapticTimer -= dt;
            if (_hapticTimer > 0f) return;
            _hapticTimer = 0.05f;
            Haptic(right, amplitude, 0.06f);
        }

        /// <summary>Fires the claw. Public so other scripts or UI can trigger a snap.</summary>
        public void Snap()
        {
            _locked = false;
            _cooldownLeft = cooldown;
            bool right = SnapIsRight;
            Haptic(right, 1f, 0.12f);
            Haptic(!right, 0.25f, 0.05f);
            if (snapAudio != null && snapClip != null)
            {
                snapAudio.pitch = Random.Range(0.95f, 1.06f);
                snapAudio.PlayOneShot(snapClip);
            }
            if (bulletPrefab != null && muzzle != null)
            {
                WaterBullet bullet = Instantiate(bulletPrefab, muzzle.position, muzzle.rotation);
                bullet.transform.localScale = bullet.transform.localScale * WorldScale;
                Transform rigRoot = trackingSpace != null ? trackingSpace.root : null;
                bullet.Launch(muzzle.forward, gameObject, transform, rigRoot);
            }
            if (onSnap != null) onSnap.Invoke();
        }

        // ------------------------------------------------------------------ pincer
        void UpdatePincer(float dt)
        {
            bool right = !SnapIsRight;
            float squeeze = Mathf.Max(ReadTrigger(right), ReadGrip(right));
            _pinchAmount = Mathf.MoveTowards(_pinchAmount, squeeze, dt / 0.06f);

            if (!_pinchClosed && squeeze > 0.7f)
            {
                _pinchClosed = true;
                TryGrab();
                Haptic(right, 0.3f, 0.03f);
            }
            else if (_pinchClosed && squeeze < 0.4f)
            {
                _pinchClosed = false;
                ReleaseHeld();
            }
        }

        void TryGrab()
        {
            if (_held != null || pinchTip == null) return;
            Collider[] near = Physics.OverlapSphere(pinchTip.position, grabRadius * WorldScale, grabMask, QueryTriggerInteraction.Ignore);
            Rigidbody best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < near.Length; i++)
            {
                Rigidbody body = near[i].attachedRigidbody;
                if (body == null || body.transform.IsChildOf(transform)) continue;
                float d = (body.worldCenterOfMass - pinchTip.position).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = body; }
            }
            if (best == null) return;
            _held = best;
            _heldWasKinematic = best.isKinematic;
            best.isKinematic = true;
            _heldLocalPos = pinchTip.InverseTransformPoint(best.transform.position);
            _heldLocalRot = Quaternion.Inverse(pinchTip.rotation) * best.transform.rotation;
        }

        void ReleaseHeld()
        {
            if (_held == null) return;
            _held.isKinematic = _heldWasKinematic;
            _held = null;
        }

        // ------------------------------------------------------------------ look
        public void SetColorway(int index)
        {
            colorway = index;
            ApplyColorway();
        }

        public void NextColorway()
        {
            if (colorways == null || colorways.Length == 0) return;
            SetColorway((colorway + 1) % colorways.Length);
        }

        void ApplyColorway()
        {
            if (bodyRenderer == null || colorways == null || colorways.Length == 0) return;
            int i = Mathf.Clamp(colorway, 0, colorways.Length - 1);
            if (colorways[i] != null) bodyRenderer.sharedMaterial = colorways[i];
        }

        void OnDrawGizmosSelected()
        {
            if (muzzle == null) return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(muzzle.position, muzzle.forward * 1.5f);
            if (pinchTip != null) Gizmos.DrawWireSphere(pinchTip.position, grabRadius * WorldScale);
        }
    }
}
