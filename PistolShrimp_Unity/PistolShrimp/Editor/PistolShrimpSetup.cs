using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

namespace PistolShrimp.EditorTools
{
    /// <summary>
    /// One-click setup. Tools > Pistol Shrimp > Build Prefabs:
    ///  - fixes the import settings of the models, palettes and sounds
    ///  - creates the three colorway materials and the water materials
    ///  - builds WaterBullet, CavitationPop and the two player prefabs (right- and left-handed)
    /// Safe to run again after changing anything. It overwrites what it generated.
    /// </summary>
    public static class PistolShrimpSetup
    {
        static readonly string[] Colorways = { "CandyStripe", "TigerOrange", "GlassRedBand" };

        [MenuItem("Tools/Pistol Shrimp/Build Prefabs")]
        public static void Build()
        {
            string root = FindRoot();
            if (root == null)
            {
                EditorUtility.DisplayDialog("Pistol Shrimp", "Could not find PistolShrimp_RightSnapper.fbx. Keep the PistolShrimp folder intact inside Assets.", "OK");
                return;
            }

            string models = root + "/Models";
            string textures = root + "/Textures";
            string audio = root + "/Audio";
            string materials = EnsureFolder(root, "Materials");
            string prefabs = EnsureFolder(root, "Prefabs");
            string animation = EnsureFolder(root, "Animation");

            string vfxPath = models + "/ShrimpVFXMeshes.fbx";
            ConfigureModel(vfxPath, false, true);
            ConfigureModel(models + "/PistolShrimp_RightSnapper.fbx", true, false);
            ConfigureModel(models + "/PistolShrimp_LeftSnapper.fbx", true, false);

            // Colorway materials: one mesh, one palette texture per look.
            Material[] bodyMats = new Material[Colorways.Length];
            for (int i = 0; i < Colorways.Length; i++)
            {
                Texture2D tex = ConfigureTexture(textures + "/ShrimpPalette_" + Colorways[i] + ".png");
                bodyMats[i] = LitMaterial(materials + "/Shrimp_" + Colorways[i] + ".mat", tex);
            }

            Material water = TransparentMaterial(materials + "/Water_Jet.mat", new Color(0.72f, 0.95f, 1f, 0.55f));
            Material flash = TransparentMaterial(materials + "/Water_Flash.mat", new Color(0.95f, 1f, 1f, 0.8f));

            AudioClip snapClip = AssetDatabase.LoadAssetAtPath<AudioClip>(audio + "/snap.wav");
            AudioClip popClip = AssetDatabase.LoadAssetAtPath<AudioClip>(audio + "/cavitation_pop.wav");

            GameObject vfxModel = AssetDatabase.LoadAssetAtPath<GameObject>(vfxPath);
            Mesh bubbleMesh = AssetDatabase.LoadAllAssetsAtPath(vfxPath).OfType<Mesh>().FirstOrDefault(m => m.name == "Bubble");

            GameObject popPrefab = BuildPop(prefabs + "/CavitationPop.prefab", vfxModel, bubbleMesh, water, flash, popClip);
            GameObject bulletPrefab = BuildBullet(prefabs + "/WaterBullet.prefab", vfxModel, bubbleMesh, water, popPrefab);

            GameObject rightPrefab = BuildPlayer(models + "/PistolShrimp_RightSnapper.fbx", prefabs + "/PistolShrimpPlayer_RightHanded.prefab",
                animation + "/ShrimpIdle_Right.controller", bodyMats, bulletPrefab, snapClip);
            BuildPlayer(models + "/PistolShrimp_LeftSnapper.fbx", prefabs + "/PistolShrimpPlayer_LeftHanded.prefab",
                animation + "/ShrimpIdle_Left.controller", bodyMats, bulletPrefab, snapClip);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (rightPrefab != null) EditorGUIUtility.PingObject(rightPrefab);
            Debug.Log("[Pistol Shrimp] Prefabs built in " + prefabs + ". Drag PistolShrimpPlayer_RightHanded into a scene that has an XR Origin.");
        }

        [MenuItem("Tools/Pistol Shrimp/Add Player To Scene")]
        public static void AddToScene()
        {
            string root = FindRoot();
            string path = root == null ? null : root + "/Prefabs/PistolShrimpPlayer_RightHanded.prefab";
            GameObject prefab = path == null ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                EditorUtility.DisplayDialog("Pistol Shrimp", "Run Tools > Pistol Shrimp > Build Prefabs first.", "OK");
                return;
            }
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(go, "Add Pistol Shrimp Player");
            Selection.activeGameObject = go;
        }

        // ------------------------------------------------------------------ assets
        static string FindRoot()
        {
            string[] guids = AssetDatabase.FindAssets("PistolShrimp_RightSnapper t:Model");
            if (guids == null || guids.Length == 0) return null;
            string modelPath = AssetDatabase.GUIDToAssetPath(guids[0]).Replace('\\', '/');
            string modelsDir = Path.GetDirectoryName(modelPath).Replace('\\', '/');
            return Path.GetDirectoryName(modelsDir).Replace('\\', '/');
        }

        static string EnsureFolder(string parent, string name)
        {
            string path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, name);
            return path;
        }

        static void ConfigureModel(string path, bool animated, bool readable)
        {
            ModelImporter imp = AssetImporter.GetAtPath(path) as ModelImporter;
            if (imp == null)
            {
                Debug.LogError("[Pistol Shrimp] Missing model: " + path);
                return;
            }
            imp.materialImportMode = ModelImporterMaterialImportMode.None;
            imp.importNormals = ModelImporterNormals.Import;   // keep the hard, flat facets from the file
            imp.importBlendShapes = false;
            imp.importCameras = false;
            imp.importLights = false;
            imp.isReadable = readable;                          // particle meshes must be readable in builds
            imp.animationType = animated ? ModelImporterAnimationType.Generic : ModelImporterAnimationType.None;
            imp.importAnimation = animated;
            if (animated)
            {
                imp.optimizeGameObjects = false;                // the scripts need the bone transforms
                ModelImporterClipAnimation[] clips = imp.defaultClipAnimations;
                if (clips != null && clips.Length > 0)
                {
                    for (int i = 0; i < clips.Length; i++)
                    {
                        clips[i].loopTime = true;
                        if (clips.Length == 1) clips[i].name = "Idle";
                    }
                    imp.clipAnimations = clips;
                }
            }
            imp.SaveAndReimport();
        }

        static Texture2D ConfigureTexture(string path)
        {
            TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null)
            {
                Debug.LogError("[Pistol Shrimp] Missing texture: " + path);
                return null;
            }
            // Palette lookups must stay exact: no filtering, no mipmaps, no compression.
            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = true;
            ti.filterMode = FilterMode.Point;
            ti.mipmapEnabled = false;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static bool UsingScriptablePipeline
        {
            get { return GraphicsSettings.currentRenderPipeline != null; }
        }

        static Material LoadOrCreate(string path, Shader shader)
        {
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            else
            {
                m.shader = shader;
            }
            return m;
        }

        static Material LitMaterial(string path, Texture2D tex)
        {
            Shader shader = null;
            if (UsingScriptablePipeline) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            Material m = LoadOrCreate(path, shader);
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
            if (m.HasProperty("_Color")) m.SetColor("_Color", Color.white);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.12f);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.12f);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material TransparentMaterial(string path, Color color)
        {
            Shader shader = null;
            if (UsingScriptablePipeline) shader = Shader.Find("Universal Render Pipeline/Unlit");
            bool urp = shader != null;
            if (!urp) shader = Shader.Find("Sprites/Default");
            Material m = LoadOrCreate(path, shader);
            if (urp)
            {
                m.SetFloat("_Surface", 1f);                     // transparent
                m.SetFloat("_Blend", 0f);                       // alpha blend
                m.SetFloat("_Cull", 0f);                        // both sides, the shock ring is a flat sheet
                m.SetFloat("_ZWrite", 0f);
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetOverrideTag("RenderType", "Transparent");
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)RenderQueue.Transparent;
                m.SetColor("_BaseColor", color);
            }
            else
            {
                m.SetColor("_Color", color);
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        // ------------------------------------------------------------------ helpers
        static Dictionary<string, Transform> MapByName(Transform root)
        {
            Dictionary<string, Transform> map = new Dictionary<string, Transform>();
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (!map.ContainsKey(all[i].name)) map.Add(all[i].name, all[i]);
            }
            return map;
        }

        static Transform Get(Dictionary<string, Transform> map, string name)
        {
            Transform t;
            if (map.TryGetValue(name, out t)) return t;
            Debug.LogError("[Pistol Shrimp] Bone not found in model: " + name);
            return null;
        }

        static Transform Marker(string name, Transform parent, Vector3 worldPos, Quaternion worldRot)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, true);
            go.transform.position = worldPos;
            go.transform.rotation = worldRot;
            return go.transform;
        }

        /// <summary>Copies one mesh object out of the VFX model, keeping the orientation the importer gave it.</summary>
        static GameObject CopyMeshObject(GameObject model, string childName, Transform parent, Material material)
        {
            Transform source = null;
            if (model != null)
            {
                Dictionary<string, Transform> map = MapByName(model.transform);
                map.TryGetValue(childName, out source);
            }
            GameObject go;
            if (source != null)
            {
                go = Object.Instantiate(source.gameObject);
            }
            else
            {
                Debug.LogWarning("[Pistol Shrimp] VFX mesh '" + childName + "' not found, using a sphere instead.");
                go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Collider col = go.GetComponent<Collider>();
                if (col != null) Object.DestroyImmediate(col);
            }
            go.name = childName;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            Renderer r = go.GetComponent<Renderer>();
            if (r != null)
            {
                r.sharedMaterial = material;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            return go;
        }

        static ParticleSystem BubbleParticles(string name, Transform parent, Mesh mesh, Material material)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
            if (mesh != null)
            {
                r.renderMode = ParticleSystemRenderMode.Mesh;
                r.mesh = mesh;
            }
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;

            ParticleSystem.MainModule main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;   // bubbles stay in the water, they do not follow the jet
            main.startColor = Color.white;
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            main.gravityModifier = -0.12f;                                // bubbles drift upward
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            return ps;
        }

        // ------------------------------------------------------------------ prefabs
        static GameObject BuildPop(string path, GameObject vfxModel, Mesh bubbleMesh, Material water, Material flash, AudioClip clip)
        {
            GameObject root = new GameObject("CavitationPop");
            CavitationPop pop = root.AddComponent<CavitationPop>();

            GameObject bubbleHolder = new GameObject("Bubble");
            bubbleHolder.transform.SetParent(root.transform, false);
            CopyMeshObject(vfxModel, "Bubble", bubbleHolder.transform, flash);

            GameObject ringHolder = new GameObject("Ring");
            ringHolder.transform.SetParent(root.transform, false);
            CopyMeshObject(vfxModel, "ShockRing", ringHolder.transform, water);

            ParticleSystem ps = BubbleParticles("Burst", root.transform, bubbleMesh, water);
            ParticleSystem.MainModule main = ps.main;
            main.loop = false;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.07f);
            main.maxParticles = 64;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.12f, (short)26) });
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.05f;
            ParticleSystem.LimitVelocityOverLifetimeModule limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.dampen = 0f;                                            // no hard speed cap, drag only
            limit.drag = 2.5f;                                            // water slows the burst quickly

            AudioSource source = root.AddComponent<AudioSource>();
            source.clip = clip;
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.minDistance = 0.6f;
            source.maxDistance = 30f;

            pop.bubble = bubbleHolder.transform;
            pop.ring = ringHolder.transform;
            pop.audioSource = source;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static GameObject BuildBullet(string path, GameObject vfxModel, Mesh bubbleMesh, Material water, GameObject popPrefab)
        {
            GameObject root = new GameObject("WaterBullet");
            WaterBullet bullet = root.AddComponent<WaterBullet>();

            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = new Vector3(0.22f, 0.22f, 0.34f);
            CopyMeshObject(vfxModel, "WaterJet", visual.transform, water);

            ParticleSystem ps = BubbleParticles("Trail", root.transform, bubbleMesh, water);
            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.35f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.045f);
            main.maxParticles = 200;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = 26f;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.03f;

            bullet.visual = visual.transform;
            bullet.trail = ps;
            bullet.popPrefab = popPrefab;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static GameObject BuildPlayer(string fbxPath, string prefabPath, string controllerPath, Material[] bodyMats, GameObject bulletPrefab, AudioClip snapClip)
        {
            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbx == null)
            {
                Debug.LogError("[Pistol Shrimp] Missing model: " + fbxPath);
                return null;
            }

            // The wrapper defines model space: +Z forward, +Y up, origin at the shrimp's centre.
            GameObject root = new GameObject(Path.GetFileNameWithoutExtension(prefabPath));
            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            model.name = "Model";
            model.transform.SetParent(root.transform, false);

            Dictionary<string, Transform> bones = MapByName(model.transform);
            Transform eyeBone = Get(bones, "EyePoint");
            Transform muzzleBone = Get(bones, "Snap_Muzzle");
            Transform snapClaw = Get(bones, "Snap_Claw");
            Transform pinchClaw = Get(bones, "Pinch_Claw");
            Transform pinchTipBone = Get(bones, "Pinch_Tip");
            if (eyeBone == null || muzzleBone == null || snapClaw == null || pinchClaw == null || pinchTipBone == null)
            {
                Object.DestroyImmediate(root);
                return null;
            }

            // Guard against an importer that brings the model in facing backwards.
            if (muzzleBone.position.z < eyeBone.position.z)
            {
                model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f) * model.transform.localRotation;
                Debug.Log("[Pistol Shrimp] Model was facing -Z, turned it around.");
            }

            SkinnedMeshRenderer smr = model.GetComponentInChildren<SkinnedMeshRenderer>();
            if (smr != null)
            {
                smr.sharedMaterial = bodyMats[0];
                smr.updateWhenOffscreen = true;     // the claws travel far from the rest pose, keep the bounds honest
            }

            Animator animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            AnimationClip idle = AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            if (idle != null)
            {
                AssetDatabase.DeleteAsset(controllerPath);
                AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPathWithClip(controllerPath, idle);
                animator.runtimeAnimatorController = controller;
            }
            else
            {
                Debug.LogWarning("[Pistol Shrimp] No idle clip found in " + fbxPath + ". The shrimp will work but will not paddle its legs.");
            }
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.applyRootMotion = false;

            Quaternion modelRot = root.transform.rotation;
            Transform eyeAnchor = Marker("EyeAnchor", eyeBone, eyeBone.position, modelRot);
            Transform muzzle = Marker("Muzzle", snapClaw, muzzleBone.position, modelRot);
            Transform pinchTip = Marker("PinchTip", pinchClaw, pinchTipBone.position, modelRot);

            AudioSource audio = muzzle.gameObject.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 1f;
            audio.minDistance = 0.5f;
            audio.maxDistance = 25f;

            PistolShrimpPlayer player = root.AddComponent<PistolShrimpPlayer>();
            player.snapperHand = snapClaw.position.x >= 0f ? SnapperHand.Right : SnapperHand.Left;
            player.snap = new ClawArm
            {
                upper = Get(bones, "Snap_ArmUpper"),
                lower = Get(bones, "Snap_ArmLower"),
                claw = snapClaw,
                dactyl = Get(bones, "Snap_Dactyl"),
                positionOffset = new Vector3(0f, -0.02f, -0.16f),
                rotationOffset = Vector3.zero
            };
            player.pinch = new ClawArm
            {
                upper = Get(bones, "Pinch_ArmUpper"),
                lower = Get(bones, "Pinch_ArmLower"),
                claw = pinchClaw,
                dactyl = Get(bones, "Pinch_Dactyl"),
                positionOffset = new Vector3(0f, -0.01f, -0.08f),
                rotationOffset = Vector3.zero
            };
            player.eyeAnchor = eyeAnchor;
            player.muzzle = muzzle;
            player.pinchTip = pinchTip;
            player.bodyRenderer = smr;
            player.colorways = bodyMats;
            player.colorway = 0;
            player.bulletPrefab = bulletPrefab != null ? bulletPrefab.GetComponent<WaterBullet>() : null;
            player.snapAudio = audio;
            player.snapClip = snapClip;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }
    }
}
