using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace MantisShrimpVR.EditorTools
{
    public static class BuildMantisReef
    {
        static System.Random random; static Material stone,sand,green,teal,orange,eye,pearl,coral;
        static float R(float a,float b){return a+(float)random.NextDouble()*(b-a);}
        static Material MakeMat(string name,Color c)
        {
            string path="Assets/MantisShrimpVR/Generated/"+name+".mat";
            var existing=AssetDatabase.LoadAssetAtPath<Material>(path);if(existing)return existing;
            Shader sh=Shader.Find("Universal Render Pipeline/Lit");if(!sh)sh=Shader.Find("Standard");
            var m=new Material(sh){name=name};if(m.HasProperty("_BaseColor"))m.SetColor("_BaseColor",c);if(m.HasProperty("_Color"))m.SetColor("_Color",c);
            AssetDatabase.CreateAsset(m,path);return m;
        }
        static Transform Group(string name,Transform parent=null){var g=new GameObject(name).transform;if(parent)g.SetParent(parent);return g;}
        static GameObject Primitive(PrimitiveType type,string name,Transform parent,Vector3 pos,Vector3 scale,Material mat,bool collision=true)
        {
            GameObject g=GameObject.CreatePrimitive(type);g.name=name;g.transform.SetParent(parent);g.transform.position=pos;g.transform.localScale=scale;
            g.GetComponent<Renderer>().sharedMaterial=mat;if(!collision)UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());return g;
        }
        static GameObject FindAsset(string name,string preference)
        {
            string[] guids=AssetDatabase.FindAssets("t:Model",new[]{"Assets"});
            var paths=guids.Select(AssetDatabase.GUIDToAssetPath).Where(p=>p.EndsWith("/"+name+".fbx",StringComparison.OrdinalIgnoreCase)).ToArray();
            var path=paths.OrderByDescending(p=>p.IndexOf(preference,StringComparison.OrdinalIgnoreCase)>=0).FirstOrDefault();
            return path==null?null:AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
        static GameObject Place(string asset,string folder,Transform parent,Vector3 at,float size,bool collider=false)
        {
            GameObject model=FindAsset(asset,folder);
            if(!model)return null;
            var root=Group(asset+"_"+parent.childCount,parent);root.position=at;root.rotation=Quaternion.Euler(0,R(0,360),0);
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(model);instance.transform.SetParent(root,false);
            instance.transform.localPosition=Vector3.zero;instance.transform.localRotation=Quaternion.identity;
            var rs=instance.GetComponentsInChildren<Renderer>();
            if(rs.Length>0)
            {
                Bounds b=rs[0].bounds;foreach(var r in rs.Skip(1))b.Encapsulate(r.bounds);
                float span=Mathf.Max(b.size.x,b.size.y,b.size.z);
                if(span>.001f)instance.transform.localScale*=size/span;
                rs=instance.GetComponentsInChildren<Renderer>();b=rs[0].bounds;foreach(var r in rs.Skip(1))b.Encapsulate(r.bounds);
                instance.transform.position+=new Vector3(at.x-b.center.x,at.y-b.min.y,at.z-b.center.z);
            }
            // Imported FBX colliders may be oversized: remove all for decoration.
            foreach(var c in root.GetComponentsInChildren<Collider>())UnityEngine.Object.DestroyImmediate(c);
            if(collider){var box=root.gameObject.AddComponent<BoxCollider>();box.center=new Vector3(0,size*.28f,0);box.size=new Vector3(size*.6f,size*.56f,size*.6f);}
            return root.gameObject;
        }
        static void Rock(Transform parent,Vector3 p,Vector3 scale,bool collision=true)
        {
            var g=Primitive(PrimitiveType.Cube,"Angular reef stone",parent,p,scale,stone,collision);
            g.transform.rotation=Quaternion.Euler(R(-12,12),R(0,180),R(-14,14));
        }
        static void Limb(Transform parent,string name,Vector3 a,Vector3 b,float thick,Material mat)
        {
            var g=Primitive(PrimitiveType.Cylinder,name,parent,(a+b)*.5f,new Vector3(thick,Vector3.Distance(a,b)*.5f,thick),mat,false);
            g.transform.rotation=Quaternion.FromToRotation(Vector3.up,b-a);
        }
        static GameObject Shrimp(Transform parent,bool armsOnly)
        {
            var root=Group(armsOnly?"First person claws":"Full body shrimp",parent);
            if(!armsOnly)
            {
                for(int i=0;i<7;i++)Primitive(PrimitiveType.Cube,"Body segment",root,new Vector3(0,-.02f,-i*.22f),new Vector3(.48f-i*.03f,.27f,.22f),i%2==0?green:teal,false);
                Primitive(PrimitiveType.Sphere,"Head",root,new Vector3(0,.08f,.2f),new Vector3(.5f,.3f,.45f),green,false);
                for(int s=-1;s<=1;s+=2)
                {
                    Limb(root,"Eye stalk",new Vector3(s*.16f,.2f,.3f),new Vector3(s*.25f,.42f,.45f),.04f,orange);
                    Primitive(PrimitiveType.Sphere,"Stalked eye",root,new Vector3(s*.25f,.43f,.45f),new Vector3(.19f,.17f,.19f),eye,false);
                    for(int i=0;i<5;i++)Limb(root,"Walking leg",new Vector3(s*.2f,-.13f,-i*.22f),new Vector3(s*.43f,-.34f,-i*.22f+.05f),.025f,orange);
                }
                for(int i=-1;i<=1;i++)Primitive(PrimitiveType.Cube,"Tail fan",root,new Vector3(i*.15f,-.03f,-1.7f),new Vector3(.18f,.07f,.4f),orange,false);
            }
            for(int s=-1;s<=1;s+=2)
            {
                Limb(root,"Raptorial upper",new Vector3(s*.18f,-.08f,.2f),new Vector3(s*.34f,-.17f,.51f),.08f,green);
                Limb(root,"Raptorial lower",new Vector3(s*.34f,-.17f,.51f),new Vector3(s*.30f,-.24f,.79f),.07f,orange);
                Primitive(PrimitiveType.Sphere,"Striking club",root,new Vector3(s*.30f,-.22f,.83f),new Vector3(.2f,.2f,.28f),teal,false);
            }
            return root.gameObject;
        }
        static Text MakeText(Transform parent,string name,Vector2 anchorMin,Vector2 anchorMax,Vector2 offset,string content,int size)
        {
            var obj=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Text));obj.transform.SetParent(parent,false);
            var rect=obj.GetComponent<RectTransform>();rect.anchorMin=anchorMin;rect.anchorMax=anchorMax;rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=offset;rect.sizeDelta=new Vector2(900,85);
            var t=obj.GetComponent<Text>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.fontSize=size;t.alignment=TextAnchor.MiddleCenter;t.color=new Color(.95f,.96f,.9f);t.text=content;
            var shadow=obj.AddComponent<Shadow>();shadow.effectColor=new Color(0,0,0,.8f);shadow.effectDistance=new Vector2(2,-2);return t;
        }
        [MenuItem("Mantis Shrimp/Build COMPLETE Clean Reef (New Scene)")]
        public static void Build()
        {
            if(EditorApplication.isPlaying){EditorUtility.DisplayDialog("Stop Play Mode","Exit Play Mode before generating the scene.","OK");return;}
            if(!EditorUtility.DisplayDialog("Create clean Mantis Shrimp reef","Creates a NEW scene. It will not modify existing scenes. Imported FBX assets are optional; placeholders are used if missing.","Build","Cancel"))return;
            random=new System.Random(2026);
            if(!AssetDatabase.IsValidFolder("Assets/MantisShrimpVR/Generated"))AssetDatabase.CreateFolder("Assets/MantisShrimpVR","Generated");
            if(!AssetDatabase.IsValidFolder("Assets/MantisShrimpVR/Scenes"))AssetDatabase.CreateFolder("Assets/MantisShrimpVR","Scenes");
            stone=MakeMat("Stone",new Color(.20f,.34f,.39f));sand=MakeMat("Sand",new Color(.62f,.67f,.58f));
            green=MakeMat("ShrimpGreen",new Color(.11f,.62f,.40f));teal=MakeMat("ShrimpTeal",new Color(.08f,.69f,.68f));
            orange=MakeMat("ShrimpOrange",new Color(.94f,.36f,.20f));eye=MakeMat("Eye",new Color(.06f,.09f,.14f));
            pearl=MakeMat("Pearl",new Color(.80f,.67f,.86f));coral=MakeMat("Coral",new Color(.83f,.42f,.63f));
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var world=Group("WORLD - Reef environment");
            Primitive(PrimitiveType.Cube,"Seabed",world,new Vector3(0,-.55f,0),new Vector3(44,1.1f,44),sand);
            var reef=Group("Coral colonies and marine life",world);
            string[] plants={"coral_branch","coral_brain","coral_fan","coral_tube","branching_coral_pink","branching_coral_purple","tube_coral","seaweed_tall","kelp","sea_anemone"};
            for(int i=0;i<105;i++)
            {
                float x=R(-19,19),z=R(-17,19);
                // Keep the central emergence path clear, especially near cave entrance.
                if(Mathf.Abs(x)<3.2f && z<1f)continue;
                string n=plants[i%plants.Length];string pref=i%plants.Length<4?"Underwater":"Aquarium";
                if(n=="kelp"||n=="seaweed_tall")pref="Underwater";
                if(Place(n,pref,reef,new Vector3(x,0,z),R(.65f,2.3f))==null)
                {var fallback=Primitive(PrimitiveType.Cylinder,"Fallback coral",reef,new Vector3(x,.35f,z),new Vector3(.22f,.35f,.22f),i%2==0?coral:teal,false);fallback.transform.rotation=Quaternion.Euler(R(-15,15),0,R(-15,15));}
            }
            for(int i=0;i<30;i++)
            {
                float a=i*Mathf.PI*2/30f;Vector3 p=new Vector3(Mathf.Cos(a)*R(16,19),0,Mathf.Sin(a)*R(16,19));
                if(Place("sea_rock_big","Underwater",reef,p,R(1.5f,3.5f))==null)Rock(reef,p+Vector3.up*.5f,new Vector3(2,1.3f,2),false);
            }
            // Cave interior occupies z=-17..-10, faces positive Z. All wall colliders are separate and cannot cover the exit.
            var cave=Group("CAVE - clear central passage (positive Z exit)",world);
            Primitive(PrimitiveType.Cube,"Cave floor",cave,new Vector3(0,-.15f,-13.5f),new Vector3(7,.3f,9),sand);
            Primitive(PrimitiveType.Cube,"Left collision wall",cave,new Vector3(-3.25f,1.5f,-13.6f),new Vector3(1.1f,3,8.5f),stone);
            Primitive(PrimitiveType.Cube,"Right collision wall",cave,new Vector3(3.25f,1.5f,-13.6f),new Vector3(1.1f,3,8.5f),stone);
            Primitive(PrimitiveType.Cube,"Ceiling collision",cave,new Vector3(0,3.45f,-13.6f),new Vector3(7,1,8.5f),stone);
            // Irregular decorative rocks: no colliders, leaving the center unobstructed.
            for(int i=0;i<18;i++)
            {
                float z=-17.3f+i*.39f;
                Rock(cave,new Vector3(-3.0f+R(-.4f,.2f),R(1,2.7f),z),new Vector3(R(.7f,1.4f),R(1,2),R(.65f,1.1f)),false);
                Rock(cave,new Vector3(3.0f+R(-.2f,.4f),R(1,2.7f),z),new Vector3(R(.7f,1.4f),R(1,2),R(.65f,1.1f)),false);
            }
            for(int i=0;i<12;i++)Rock(cave,new Vector3(R(-3,3),3.1f+R(-.1f,.5f),R(-17.3f,-10.2f)),new Vector3(R(.7f,1.6f),R(.5f,.9f),R(.7f,1.6f)),false);
            // Arch over the cave exit; the 4m wide center is completely open.
            Rock(cave,new Vector3(-2.8f,1.6f,-9.25f),new Vector3(1.8f,3.3f,1.6f),false);
            Rock(cave,new Vector3(2.8f,1.6f,-9.25f),new Vector3(1.8f,3.3f,1.6f),false);
            Rock(cave,new Vector3(0,3.3f,-9.25f),new Vector3(4.5f,1.1f,1.8f),false);
            var exit=Group("CaveExitMarker - narration trigger",cave);exit.position=new Vector3(0,.7f,-8.1f);
            var player=Group("PLAYER - Desktop mantis shrimp");player.position=new Vector3(0,.1f,-16.1f);
            var cc=player.gameObject.AddComponent<CharacterController>();cc.height=1.1f;cc.radius=.28f;cc.center=new Vector3(0,.55f,0);cc.stepOffset=.2f;
            var cameraGo=new GameObject("First Person Camera",typeof(Camera),typeof(AudioListener));cameraGo.transform.SetParent(player,false);cameraGo.transform.localPosition=new Vector3(0,.75f,0);
            var camera=cameraGo.GetComponent<Camera>();camera.fieldOfView=75;camera.nearClipPlane=.03f;camera.farClipPlane=90;
            var claws=Shrimp(cameraGo.transform,true);claws.transform.localPosition=new Vector3(0,-.48f,.45f);claws.transform.localScale=Vector3.one*.58f;
            var canvasGo=new GameObject("Desktop narration HUD",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            var canvas=canvasGo.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=canvasGo.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1600,900);
            var narrative=MakeText(canvasGo.transform,"Narration",new Vector2(.5f,.09f),new Vector2(.5f,.09f),Vector2.zero,"",23);
            var filterLabel=MakeText(canvasGo.transform,"Vision label",new Vector2(.5f,.93f),new Vector2(.5f,.93f),Vector2.zero,"",20);
            var scoreLabel=MakeText(canvasGo.transform,"Score",new Vector2(.86f,.9f),new Vector2(.86f,.9f),Vector2.zero,"FOOD + 0",20);
            var vision=player.gameObject.AddComponent<MantisVision>();vision.target=camera;vision.label=filterLabel;
            var movement=player.gameObject.AddComponent<MantisDesktopPlayer>();movement.view=cameraGo.transform;movement.narration=narrative;movement.vision=vision;
            MantisPrey.scoreText=scoreLabel;
            var reflection=Group("REFLECTION COVE - staged identity reveal",world);reflection.position=new Vector3(7,0,9);
            Primitive(PrimitiveType.Cube,"Pearlescent shell surface",reflection,new Vector3(7,1.2f,9),new Vector3(2.7f,2.1f,.2f),pearl,false);
            var shrimp=Shrimp(reflection,false);shrimp.transform.localPosition=new Vector3(0,1.2f,-.65f);shrimp.transform.localRotation=Quaternion.Euler(0,180,0);shrimp.transform.localScale=Vector3.one*.85f;
            var reveal=reflection.gameObject.AddComponent<MantisReveal>();reveal.player=player;reveal.shrimp=shrimp;
            shrimp.SetActive(false);
            var intro=Group("CaveIntroduction - no automatic movement");var narration=intro.gameObject.AddComponent<MantisNarration>();
            narration.player=player;narration.exitMarker=exit;narration.reflection=reflection;narration.text=narrative;
            var prey=Group("PREY - click to strike",world);
            for(int i=0;i<6;i++)
            {
                Vector3 p=new Vector3(i%2==0?-6:5,0,1+i*2.5f);
                string n=i%2==0?"crab":"clam_shell";var obj=Place(n,"Underwater",prey,p,1.2f);
                if(obj==null)obj=Primitive(PrimitiveType.Sphere,"Placeholder prey",prey,p+Vector3.up*.35f,new Vector3(.8f,.7f,.8f),orange);
                obj.AddComponent<MantisPrey>();
                if(!obj.GetComponentInChildren<Collider>()) {var c=obj.AddComponent<SphereCollider>();c.center=new Vector3(0,.4f,0);c.radius=.65f;}
            }
            var fish=Group("ANIMATED FISH - ambient swimming",world);
            for(int i=0;i<10;i++)
            {
                Vector3 p=new Vector3(R(-12,12),R(1.4f,4),R(-3,17));
                var f=Place("Fish"+(i%3+1),"Quaternius",fish,p,R(.7f,1.4f));
                if(f==null)f=Place("fish_small","Underwater",fish,p,1);
                if(f!=null)f.AddComponent<MantisFishSwim>();
            }
            var net=Group("NET - slow chase prototype",world);net.position=new Vector3(10,0,15);
            var netObj=Place("fish_net","Aquarium",net,new Vector3(10,1.4f,15),3f);
            if(netObj==null)Primitive(PrimitiveType.Cube,"Placeholder net",net,new Vector3(10,1.4f,15),new Vector3(2.5f,2,.15f),orange,false);
            var chase=net.gameObject.AddComponent<MantisNetChase>();chase.player=player;
            var lightGo=new GameObject("Underwater sun",typeof(Light));var light=lightGo.GetComponent<Light>();light.type=LightType.Directional;light.intensity=1.2f;light.color=new Color(.79f,.9f,.89f);lightGo.transform.rotation=Quaternion.Euler(48,-25,0);
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight=new Color(.48f,.63f,.65f);
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogDensity=.026f;RenderSettings.fogColor=new Color(.045f,.27f,.35f);
            string path="Assets/MantisShrimpVR/Scenes/MantisReef_Clean.unity";
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),path);
            AssetDatabase.SaveAssets();Selection.activeGameObject=player.gameObject;
            Debug.Log("MantisShrimpVR: Built clean reef at "+path+". Start in the cave, move with WASD. No automatic movement.");
            EditorUtility.DisplayDialog("Reef generated","Scene saved at "+path+". Press Play to test. See README for controls and limitations.","OK");
        }
    }
}
