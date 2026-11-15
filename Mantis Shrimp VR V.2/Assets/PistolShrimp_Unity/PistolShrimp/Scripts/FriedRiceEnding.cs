using System.Collections.Generic;
using UnityEngine;

namespace PistolShrimp
{
    /// <summary>
    /// The last scene after the net catches you: a bowl of shrimp fried rice that
    /// assembles itself. Bowl first, then rice, then the toppings rain in, and the
    /// shrimp land last. Built from primitives in code, so it needs no assets.
    /// </summary>
    public class FriedRiceEnding : MonoBehaviour
    {
        class Piece
        {
            public Transform t;
            public Vector3 rest;
            public Vector3 scale;
            public float delay;
            public float drop;
            public bool thump;
            public bool landed;
        }

        readonly List<Piece> _pieces = new List<Piece>();
        readonly Dictionary<Color, Material> _materials = new Dictionary<Color, Material>();
        readonly List<Transform> _steam = new List<Transform>();
        readonly List<Material> _steamMaterials = new List<Material>();
        Transform _spin;
        float _time;
        Shader _lit;

        static readonly Color Bowl = new Color(0.80f, 0.16f, 0.14f);
        static readonly Color BowlRim = new Color(0.95f, 0.93f, 0.88f);
        static readonly Color Plate = new Color(0.20f, 0.22f, 0.26f);
        static readonly Color Rice = new Color(0.93f, 0.82f, 0.56f);
        static readonly Color RiceLight = new Color(0.98f, 0.93f, 0.78f);
        static readonly Color RiceDark = new Color(0.80f, 0.64f, 0.36f);
        static readonly Color Pea = new Color(0.36f, 0.72f, 0.26f);
        static readonly Color Carrot = new Color(0.95f, 0.50f, 0.12f);
        static readonly Color Egg = new Color(0.98f, 0.84f, 0.25f);
        static readonly Color Scallion = new Color(0.55f, 0.85f, 0.45f);
        static readonly Color Shrimp = new Color(0.97f, 0.52f, 0.38f);
        static readonly Color ShrimpLight = new Color(1.00f, 0.78f, 0.66f);
        static readonly Color ShrimpTail = new Color(0.86f, 0.22f, 0.16f);
        static readonly Color Stick = new Color(0.45f, 0.28f, 0.14f);

        const float DomeRadius = 0.2f;
        const float DomeHeight = 0.12f;
        const float DomeBase = 0.03f;

        void Awake()
        {
            _lit = Shader.Find("Universal Render Pipeline/Lit");
            if (_lit == null) _lit = Shader.Find("Sprites/Default");

            // Tilt the bowl toward the player so they look into it, and spin a child so the tilt stays put.
            transform.localRotation = Quaternion.Euler(-32f, 0f, 0f);
            GameObject spin = new GameObject("Spin");
            spin.transform.SetParent(transform, false);
            _spin = spin.transform;

            System.Random rng = new System.Random(5);

            // Plate and bowl.
            Add(PrimitiveType.Cylinder, Plate, new Vector3(0f, -0.085f, 0f), new Vector3(0.62f, 0.008f, 0.62f), Quaternion.identity, 0f, 0.5f, false);
            Add(PrimitiveType.Sphere, Bowl, new Vector3(0f, -0.02f, 0f), new Vector3(0.5f, 0.2f, 0.5f), Quaternion.identity, 0.15f, 0.6f, true);
            Add(PrimitiveType.Cylinder, BowlRim, new Vector3(0f, 0.035f, 0f), new Vector3(0.47f, 0.012f, 0.47f), Quaternion.identity, 0.15f, 0.6f, false);

            // The rice mound.
            Add(PrimitiveType.Sphere, Rice, new Vector3(0f, DomeBase, 0f), new Vector3(DomeRadius * 2f, DomeHeight * 2f, DomeRadius * 2f), Quaternion.identity, 0.7f, 0.5f, true);

            // Grains on the surface.
            for (int i = 0; i < 46; i++)
            {
                Vector3 p = OnDome(rng, 75f);
                Color c = i % 3 == 0 ? RiceDark : RiceLight;
                Add(PrimitiveType.Capsule, c, p, new Vector3(0.012f, 0.014f, 0.012f), RandomRotation(rng), 1.1f + (float)rng.NextDouble() * 0.9f, 0.45f, false);
            }
            // Toppings.
            for (int i = 0; i < 12; i++)
                Add(PrimitiveType.Sphere, Pea, OnDome(rng, 70f), Vector3.one * 0.024f, Quaternion.identity, 1.7f + (float)rng.NextDouble() * 0.8f, 0.5f, false);
            for (int i = 0; i < 10; i++)
                Add(PrimitiveType.Cube, Carrot, OnDome(rng, 70f), Vector3.one * 0.021f, RandomRotation(rng), 1.9f + (float)rng.NextDouble() * 0.8f, 0.5f, false);
            for (int i = 0; i < 6; i++)
                Add(PrimitiveType.Sphere, Egg, OnDome(rng, 60f), new Vector3(0.05f, 0.016f, 0.038f), RandomRotation(rng), 2.1f + (float)rng.NextDouble() * 0.7f, 0.5f, false);
            for (int i = 0; i < 9; i++)
                Add(PrimitiveType.Cylinder, Scallion, OnDome(rng, 65f), new Vector3(0.016f, 0.004f, 0.016f), RandomRotation(rng), 2.3f + (float)rng.NextDouble() * 0.7f, 0.5f, false);

            // Three curled shrimp, last and with the biggest landing.
            float[] yaw = { 20f, 150f, 265f };
            for (int s = 0; s < yaw.Length; s++)
            {
                float a = yaw[s] * Mathf.Deg2Rad;
                Vector3 centre = new Vector3(Mathf.Cos(a) * 0.085f, DomeBase + DomeHeight * 0.93f, Mathf.Sin(a) * 0.085f);
                AddShrimp(centre, yaw[s] + 40f, 3.2f + s * 0.45f);
            }

            // Chopsticks resting on the rim.
            Add(PrimitiveType.Cylinder, Stick, new Vector3(0.20f, 0.07f, -0.02f), new Vector3(0.012f, 0.2f, 0.012f), Quaternion.Euler(0f, 0f, 68f) * Quaternion.Euler(0f, 20f, 0f), 4.8f, 0.4f, false);
            Add(PrimitiveType.Cylinder, Stick, new Vector3(0.20f, 0.07f, 0.03f), new Vector3(0.012f, 0.2f, 0.012f), Quaternion.Euler(0f, 0f, 64f) * Quaternion.Euler(0f, -15f, 0f), 4.95f, 0.4f, false);

            // Steam.
            Shader soft = Shader.Find("Sprites/Default");
            for (int i = 0; i < 4; i++)
            {
                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "Steam";
                Collider col = go.GetComponent<Collider>();
                if (col != null) Destroy(col);
                go.transform.SetParent(transform, false);
                Material m = new Material(soft);
                go.GetComponent<Renderer>().sharedMaterial = m;
                go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _steam.Add(go.transform);
                _steamMaterials.Add(m);
            }
        }

        void AddShrimp(Vector3 centre, float yawDegrees, float delay)
        {
            // A curl of shrinking beads with a small red tail fan.
            Quaternion yaw = Quaternion.Euler(0f, yawDegrees, 0f);
            const int beads = 6;
            for (int i = 0; i < beads; i++)
            {
                float a = (-20f + i * 42f) * Mathf.Deg2Rad;
                Vector3 local = new Vector3(Mathf.Cos(a) * 0.036f, 0.012f, Mathf.Sin(a) * 0.036f);
                float size = Mathf.Lerp(0.036f, 0.016f, i / (float)(beads - 1));
                Add(PrimitiveType.Sphere, i % 2 == 0 ? Shrimp : ShrimpLight, centre + yaw * local, Vector3.one * size, Quaternion.identity, delay, 0.7f, i == 0);
            }
            float t = (-20f + beads * 42f) * Mathf.Deg2Rad;
            Vector3 tail = new Vector3(Mathf.Cos(t) * 0.036f, 0.012f, Mathf.Sin(t) * 0.036f);
            Add(PrimitiveType.Cube, ShrimpTail, centre + yaw * tail, new Vector3(0.022f, 0.006f, 0.016f), yaw * Quaternion.Euler(0f, 30f, 25f), delay, 0.7f, false);
        }

        Vector3 OnDome(System.Random rng, float maxAngle)
        {
            float theta = Mathf.Sqrt((float)rng.NextDouble()) * maxAngle * Mathf.Deg2Rad;   // sqrt spreads them evenly
            float phi = (float)rng.NextDouble() * Mathf.PI * 2f;
            return new Vector3(
                Mathf.Sin(theta) * Mathf.Cos(phi) * DomeRadius,
                DomeBase + Mathf.Cos(theta) * DomeHeight + 0.004f,
                Mathf.Sin(theta) * Mathf.Sin(phi) * DomeRadius);
        }

        static Quaternion RandomRotation(System.Random rng)
        {
            return Quaternion.Euler((float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 360f);
        }

        Material MaterialFor(Color color)
        {
            Material m;
            if (_materials.TryGetValue(color, out m)) return m;
            m = new Material(_lit);
            m.color = color;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.15f);
            _materials.Add(color, m);
            return m;
        }

        void Add(PrimitiveType type, Color color, Vector3 rest, Vector3 scale, Quaternion rotation, float delay, float drop, bool thump)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            Collider col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);              // decoration only, the water jet should pass through
            go.transform.SetParent(_spin, false);
            go.transform.localRotation = rotation;
            go.transform.localScale = Vector3.zero;
            Renderer r = go.GetComponent<Renderer>();
            r.sharedMaterial = MaterialFor(color);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            Piece p = new Piece();
            p.t = go.transform;
            p.rest = rest;
            p.scale = scale;
            p.delay = delay;
            p.drop = drop;
            p.thump = thump;
            _pieces.Add(p);
        }

        void Update()
        {
            _time += Time.deltaTime;
            if (_spin != null) _spin.localRotation = Quaternion.Euler(0f, _time * 22f, 0f);

            const float fall = 0.32f;
            for (int i = 0; i < _pieces.Count; i++)
            {
                Piece p = _pieces[i];
                float t = _time - p.delay;
                if (t < 0f) continue;
                float y;
                if (t < fall)
                {
                    float u = t / fall;
                    y = p.drop * (1f - u * u);                                   // falls faster as it nears the bowl
                }
                else
                {
                    float b = t - fall;
                    y = 0.028f * Mathf.Exp(-b * 9f) * Mathf.Abs(Mathf.Sin(b * 17f));   // two small bounces
                    if (!p.landed)
                    {
                        p.landed = true;
                        if (p.thump)
                        {
                            ShrimpSettings.Haptic(true, 0.8f, 0.07f);
                            ShrimpSettings.Haptic(false, 0.8f, 0.07f);
                        }
                    }
                }
                p.t.localPosition = p.rest + Vector3.up * y;
                p.t.localScale = p.scale * Mathf.Clamp01(t / 0.12f);
            }

            // Steam rises from the rice once the shrimp are in.
            for (int i = 0; i < _steam.Count; i++)
            {
                float t = _time - 4.6f - i * 0.55f;
                bool on = t > 0f;
                _steam[i].gameObject.SetActive(on);
                if (!on) continue;
                float k = Mathf.Repeat(t / 2.2f, 1f);
                float side = Mathf.Sin((t + i) * 1.7f) * 0.03f;
                _steam[i].localPosition = new Vector3(-0.06f + i * 0.04f + side, 0.2f + k * 0.22f, 0f);
                _steam[i].localScale = Vector3.one * Mathf.Lerp(0.03f, 0.075f, k);
                _steamMaterials[i].color = new Color(1f, 1f, 1f, 0.3f * Mathf.Sin(k * Mathf.PI));
            }
        }

        void OnDestroy()
        {
            foreach (Material m in _materials.Values) if (m != null) Destroy(m);
            for (int i = 0; i < _steamMaterials.Count; i++) if (_steamMaterials[i] != null) Destroy(_steamMaterials[i]);
        }
    }
}
