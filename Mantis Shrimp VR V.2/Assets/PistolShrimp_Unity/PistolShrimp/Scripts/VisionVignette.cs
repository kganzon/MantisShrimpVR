using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PistolShrimp
{
    /// <summary>
    /// Darkens the edges of the player's view, leaving a clear window in the middle.
    /// Add it to the PistolShrimpPlayer object (or any object). It builds a dome around
    /// the headset camera, so it costs almost nothing on Quest and needs no post-processing.
    /// </summary>
    public class VisionVignette : MonoBehaviour
    {
        public Color color = new Color(0.13f, 0.07f, 0.03f, 1f);   // dark brown
        [Tooltip("Degrees from the centre of view where the darkening starts.")]
        [Range(0f, 80f)] public float clearAngle = 20f;
        [Tooltip("Degrees from the centre of view where the darkening is at full strength.")]
        [Range(5f, 90f)] public float fullAngle = 46f;
        [Tooltip("How dark the edge gets. 1 = fully opaque.")]
        [Range(0f, 1f)] public float strength = 0.95f;
        [Tooltip("Empty = Camera.main.")]
        public Transform head;

        GameObject _dome;
        Mesh _mesh;
        Material _material;
        Color _builtColor;
        float _builtClear = -1f, _builtFull = -1f, _builtStrength = -1f;

        const int Rings = 36;        // one ring every 5 degrees
        const int Segments = 32;
        const float Radius = 1.5f;

        void OnDisable()
        {
            if (_dome != null) Destroy(_dome);
            if (_mesh != null) Destroy(_mesh);
            if (_material != null) Destroy(_material);
            _dome = null; _mesh = null; _material = null;
            _builtClear = -1f;
        }

        void LateUpdate()
        {
            if (head == null && Camera.main != null) head = Camera.main.transform;
            if (head == null) return;
            if (_dome == null) Create();
            if (_dome.transform.parent != head)
            {
                _dome.transform.SetParent(head, false);
                _dome.transform.localPosition = Vector3.zero;
                _dome.transform.localRotation = Quaternion.identity;
            }
            if (color != _builtColor || clearAngle != _builtClear || fullAngle != _builtFull || strength != _builtStrength)
            {
                Paint();
            }
        }

        void Create()
        {
            _dome = new GameObject("VisionVignette");
            _dome.transform.SetParent(head, false);

            // A full sphere of vertices around the head. Ring 0 points straight ahead.
            List<Vector3> verts = new List<Vector3>();
            List<int> tris = new List<int>();
            for (int r = 0; r <= Rings; r++)
            {
                float theta = Mathf.PI * r / Rings;
                for (int s = 0; s <= Segments; s++)
                {
                    float phi = 2f * Mathf.PI * s / Segments;
                    Vector3 dir = new Vector3(Mathf.Sin(theta) * Mathf.Cos(phi), Mathf.Sin(theta) * Mathf.Sin(phi), Mathf.Cos(theta));
                    verts.Add(dir * Radius);
                }
            }
            int row = Segments + 1;
            for (int r = 0; r < Rings; r++)
            {
                for (int s = 0; s < Segments; s++)
                {
                    int a = r * row + s, b = a + 1, c = a + row, d = c + 1;
                    tris.Add(a); tris.Add(c); tris.Add(b);
                    tris.Add(b); tris.Add(c); tris.Add(d);
                }
            }
            _mesh = new Mesh();
            _mesh.name = "VisionVignette";
            _mesh.SetVertices(verts);
            _mesh.SetTriangles(tris, 0);
            _mesh.bounds = new Bounds(Vector3.zero, Vector3.one * Radius * 2.2f);

            // UI/Default draws both sides, uses vertex colours, and can be told to ignore depth
            // so the vignette always sits on top of the scene.
            Shader shader = Shader.Find("UI/Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            _material = new Material(shader);
            _material.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
            _material.renderQueue = 4000;

            _dome.AddComponent<MeshFilter>().sharedMesh = _mesh;
            MeshRenderer renderer = _dome.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        void Paint()
        {
            float full = Mathf.Max(fullAngle, clearAngle + 1f);
            Color[] colors = new Color[(Rings + 1) * (Segments + 1)];
            int i = 0;
            for (int r = 0; r <= Rings; r++)
            {
                float angle = 180f * r / Rings;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(clearAngle, full, angle));
                Color c = color;
                c.a = k * strength;
                for (int s = 0; s <= Segments; s++) colors[i++] = c;
            }
            _mesh.colors = colors;
            _builtColor = color; _builtClear = clearAngle; _builtFull = fullAngle; _builtStrength = strength;
        }
    }
}
