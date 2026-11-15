using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MantisShrimpVR.EditorTools
{
    // Additive editor-only upgrade. No duplicate runtime class definitions.
    public static class UpgradeReefV6
    {
        static System.Random rng;
        static float Rand(float a,float b) { return a+(float)rng.NextDouble()*(b-a); }
        static Transform Child(Transform root,string name) { return root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name==name); }
        static Transform FindPart(string part) { return UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).FirstOrDefault(t=>t.name.Contains(part)); }
        static Material Mat(string name,Color color)
        {
            string folder="Assets/MantisShrimpVR/Generated";
            if(!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/MantisShrimpVR","Generated");
            string path=folder+"/V6_"+name+".mat";
            Material m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m)return m;
            Shader shader=Shader.Find("Universal Render Pipeline/Lit");if(!shader)shader=Shader.Find("Standard");
            m=new Material(shader);m.name="V6_"+name;
            if(m.HasProperty("_BaseColor"))m.SetColor("_BaseColor",color);
            if(m.HasProperty("_Color"))m.SetColor("_Color",color);
            AssetDatabase.CreateAsset(m,path);return m;
        }
        static GameObject Model(string name,string preference,Transform parent,Vector3 position,float size)
        {
            string[] paths=AssetDatabase.FindAssets("t:Model").Select(AssetDatabase.GUIDToAssetPath)
                .Where(p=>p.EndsWith("/"+name+".fbx",StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(p=>p.IndexOf(preference,StringComparison.OrdinalIgnoreCase)>=0).ToArray();
            if(paths.Length==0)return null;
            GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>(paths[0]);if(!prefab)return null;
            GameObject root=new GameObject("V6 "+name);root.transform.SetParent(parent);root.transform.position=position;
            root.transform.rotation=Quaternion.Euler(0,Rand(0,360),0);
            GameObject instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab);instance.transform.SetParent(root.transform,false);
            var rs=instance.GetComponentsInChildren<Renderer>();
            if(rs.Length>0)
            {
                Bounds b=rs[0].bounds;foreach(var r in rs.Skip(1))b.Encapsulate(r.bounds);
                float span=Mathf.Max(b.size.x,b.size.y,b.size.z);
                if(span>.001f)instance.transform.localScale*=size/span;
                rs=instance.GetComponentsInChildren<Renderer>();b=rs[0].bounds;foreach(var r in rs.Skip(1))b.Encapsulate(r.bounds);
                instance.transform.position+=new Vector3(position.x-b.center.x,position.y-b.min.y,position.z-b.center.z);
            }
            foreach(var c in root.GetComponentsInChildren<Collider>())UnityEngine.Object.DestroyImmediate(c);
            return root;
        }
        static GameObject Primitive(PrimitiveType type,string name,Transform parent,Vector3 pos,Vector3 scale,Material material)
        {
            GameObject g=GameObject.CreatePrimitive(type);g.name=name;g.transform.SetParent(parent);g.transform.position=pos;g.transform.localScale=scale;
            g.GetComponent<Renderer>().sharedMaterial=material;
            UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
            return g;
        }
        static void Segment(string name,Transform parent,Vector3 a,Vector3 b,float radius,Material material)
        {
            GameObject g=Primitive(PrimitiveType.Cylinder,name,parent,(a+b)*.5f,new Vector3(radius,Vector3.Distance(a,b)*.5f,radius),material);
            g.transform.rotation=Quaternion.FromToRotation(Vector3.up,b-a);
        }
        static bool Inside(Vector2 p,Vector2[] poly)
        {
            bool c=false;for(int i=0,j=poly.Length-1;i<poly.Length;j=i++)
            {if((poly[i].y>p.y)!=(poly[j].y>p.y) && p.x<(poly[j].x-poly[i].x)*(p.y-poly[i].y)/(poly[j].y-poly[i].y)+poly[i].x)c=!c;}return c;
        }
        static void Fill(Color32[] pixels,int w,int h,Vector2[] vertices,Color32 color)
        {
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)if(Inside(new Vector2(x,y),vertices))pixels[y*w+x]=color;
        }
        static Vector2[] Polygon(params float[] values)
        {
            Vector2[] result=new Vector2[values.Length/2];for(int i=0;i<result.Length;i++)result[i]=new Vector2(values[i*2],values[i*2+1]);return result;
        }
        static Texture2D Portrait()
        {
            const int w=512,h=512;Color32[] pix=new Color32[w*h];
            for(int i=0;i<pix.Length;i++)pix[i]=new Color32(172,209,208,255);
            // Flat, stylized 2D mantis shrimp portrait. No 3D shrimp emerges from the shell.
            Color32 teal=new Color32(22,160,144,255),green=new Color32(45,186,118,255),orange=new Color32(240,116,71,255),dark=new Color32(25,51,72,255),pink=new Color32(226,152,195,255);
            Fill(pix,w,h,Polygon(220,90,285,90,327,135,303,205,216,209,183,145),orange);
            for(int i=0;i<5;i++)
            {
                int y=165+i*41;
                Fill(pix,w,h,Polygon(174+i*4,y,337-i*4,y-6,354-i*4,y+29,315-i*3,y+44,181+i*3,y+36),i%2==0?green:teal);
            }
            Fill(pix,w,h,Polygon(175,164,208,123,306,123,338,166,310,208,198,213),green);
            Fill(pix,w,h,Polygon(198,130,167,94,178,65,218,102),orange);
            Fill(pix,w,h,Polygon(304,131,338,91,335,65,293,102),orange);
            Fill(pix,w,h,Polygon(165,71,192,55,214,78,187,99),dark);
            Fill(pix,w,h,Polygon(307,79,330,55,353,72,330,100),dark);
            Fill(pix,w,h,Polygon(180,225,134,247,123,312,153,293,202,248),orange);
            Fill(pix,w,h,Polygon(327,225,375,249,385,313,355,293,307,248),orange);
            Fill(pix,w,h,Polygon(190,363,151,432,205,404,246,365),pink);
            Fill(pix,w,h,Polygon(253,367,301,438,353,416,322,365),pink);
            Texture2D tex=new Texture2D(w,h,TextureFormat.RGBA32,false);tex.name="V6_FlatShrimpPortrait";tex.SetPixels32(pix);tex.filterMode=FilterMode.Bilinear;tex.Apply();return tex;
        }
        [MenuItem("Mantis Shrimp/Upgrade to v6 - Reef, Flat Reflection, VR Preparation")]
        public static void Build()
        {
            if(EditorApplication.isPlaying){EditorUtility.DisplayDialog("Stop Play Mode","Exit Play mode before upgrading.","OK");return;}
            if(!EditorUtility.DisplayDialog("Build Reef v6","Upgrades the OPEN scene and saves a separate v6 scene. Your original scene remains unchanged. Existing asset imports are reused.","Continue","Cancel"))return;
            rng=new System.Random(2606);
            Transform world=FindPart("WORLD - Reef environment");
            Transform player=FindPart("PLAYER - Desktop mantis shrimp");
            if(!world || !player){EditorUtility.DisplayDialog("Open clean reef first","Open MantisReef_Clean.unity before using this upgrade.","OK");return;}
            Transform old=Child(world,"V6 - Enriched marine habitat");if(old)UnityEngine.Object.DestroyImmediate(old.gameObject);
            Transform parent=new GameObject("V6 - Enriched marine habitat").transform;parent.SetParent(world);
            Material stone=Mat("Rock",new Color(.19f,.34f,.39f)),sand=Mat("Shell",new Color(.81f,.74f,.59f));
            Material rope=Mat("NetRope",new Color(.72f,.72f,.60f)),handle=Mat("NetHandle",new Color(.49f,.28f,.13f));
            // Denser but readable reef: keep the emergence lane open.
            string[] decor={"coral_branch","coral_brain","coral_fan","coral_table","coral_tube","sea_anemone","kelp","seaweed_tall","seaweed_bush","conch_shell","starfish"};
            int placed=0;
            for(int i=0;i<145;i++)
            {
                float x=Rand(-19,19),z=Rand(-7,19);
                if(Mathf.Abs(x)<3.1f && z<1.5f)continue;
                string name=decor[i%decor.Length];
                if(Model(name,"Underwater",parent,new Vector3(x,0,z),Rand(.6f,2.3f))!=null)placed++;
            }
            for(int i=0;i<32;i++)
            {
                float a=Rand(0,Mathf.PI*2),radius=Rand(9,19),x=Mathf.Cos(a)*radius,z=Mathf.Sin(a)*radius;
                if(Mathf.Abs(x)<3.1f && z<1.5f)continue;
                if(Model("sea_rock_small","Underwater",parent,new Vector3(x,0,z),Rand(.7f,2f))!=null)placed++;
            }
            // Fish school uses existing MantisFishSwim runtime script, avoiding new classes.
            Transform school=new GameObject("V6 - Additional fish schools").transform;school.SetParent(parent);
            int fish=0;
            for(int i=0;i<44;i++)
            {
                float x=Rand(-16,16),z=Rand(-3,18),y=Rand(1.4f,5.4f);
                if(Mathf.Abs(x)<3 && z<1)continue;
                string name=i%6==0?"fish_clown":i%6==1?"fish_small":"Fish"+(i%3+1);
                GameObject f=Model(name,i%6<2?"Underwater":"Quaternius",school,new Vector3(x,y,z),Rand(.35f,.9f));
                if(!f)continue;
                var motion=f.AddComponent<MantisShrimpVR.MantisFishSwim>();motion.radius=Rand(.7f,2.8f);motion.speed=Rand(.22f,.55f);fish++;
            }
            // Replace staged popping 3D model with a flat image on the existing shell.
            Transform reflection=FindPart("REFLECTION COVE");
            if(reflection)
            {
                var reveal=reflection.GetComponent<MantisShrimpVR.MantisReveal>();
                if(reveal){if(reveal.shrimp)reveal.shrimp.SetActive(false);UnityEngine.Object.DestroyImmediate(reveal);}
                foreach(var t in reflection.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Full body shrimp").ToArray())UnityEngine.Object.DestroyImmediate(t.gameObject);
                string path="Assets/MantisShrimpVR/Generated/V6_FlatShrimpPortrait.png";
                byte[] png=Portrait().EncodeToPNG();System.IO.File.WriteAllBytes(path,png);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
                Texture2D tex=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Shader unlit=Shader.Find("Universal Render Pipeline/Unlit");if(!unlit)unlit=Shader.Find("Unlit/Transparent");
                Material flat=new Material(unlit);flat.name="V6_FlatReflection";
                if(flat.HasProperty("_BaseMap"))flat.SetTexture("_BaseMap",tex);
                if(flat.HasProperty("_MainTex"))flat.SetTexture("_MainTex",tex);
                if(flat.HasProperty("_Surface"))flat.SetFloat("_Surface",1);
                flat.renderQueue=3000;
                AssetDatabase.CreateAsset(flat,"Assets/MantisShrimpVR/Generated/V6_FlatReflection.mat");
                GameObject panel=GameObject.CreatePrimitive(PrimitiveType.Quad);panel.name="V6 - FLAT 2D shrimp reflection";panel.transform.SetParent(reflection,false);
                panel.transform.position=new Vector3(7,1.2f,8.865f);panel.transform.rotation=Quaternion.Euler(0,180,0);panel.transform.localScale=new Vector3(1.65f,1.65f,1);
                panel.GetComponent<MeshRenderer>().sharedMaterial=flat;UnityEngine.Object.DestroyImmediate(panel.GetComponent<Collider>());
            }
            // Rebuild the fishing net with handle ABOVE the rim and the mouth facing the player.
            Transform net=FindPart("NET - slow chase prototype");
            if(net)
            {
                foreach(Transform child in net.Cast<Transform>().ToArray())UnityEngine.Object.DestroyImmediate(child.gameObject);
                Vector3 center=net.position;
                // Local +Z is the catching direction, and the net opening is a vertical ellipse.
                Transform netMesh=new GameObject("V6 - Net opening faces player").transform;netMesh.SetParent(net,false);
                float rx=1.2f,ry=.85f;
                for(int i=0;i<20;i++)
                {
                    float a=i*Mathf.PI*2/20f,b=(i+1)*Mathf.PI*2/20f;
                    Vector3 p=new Vector3(Mathf.Cos(a)*rx,1.7f+Mathf.Sin(a)*ry,0);
                    Vector3 q=new Vector3(Mathf.Cos(b)*rx,1.7f+Mathf.Sin(b)*ry,0);
                    Segment("Net rim",netMesh,center+p,center+q,.035f,rope);
                }
                for(int i=-4;i<=4;i++)
                {
                    float x=i*.22f,lim=Mathf.Sqrt(Mathf.Max(0,1-x*x/(rx*rx)))*ry;
                    Segment("Net vertical cord",netMesh,center+new Vector3(x,1.7f-lim,0),center+new Vector3(x,1.7f+lim,0),.008f,rope);
                }
                for(int j=-3;j<=3;j++)
                {
                    float y=j*.2f,lim=Mathf.Sqrt(Mathf.Max(0,1-y*y/(ry*ry)))*rx;
                    Segment("Net horizontal cord",netMesh,center+new Vector3(-lim,1.7f+y,0),center+new Vector3(lim,1.7f+y,0),.008f,rope);
                }
                Segment("Handle ABOVE net",netMesh,center+new Vector3(0,2.55f,0),center+new Vector3(0,4.4f,0),.075f,handle);
                net.rotation=Quaternion.identity;
                // Original chase script continues moving the parent. New net graphic remains oriented toward approach corridor.
            }
            // Slightly stronger depth cue, without changing sensory toggle runtime script.
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogDensity=.034f;
            RenderSettings.fogColor=new Color(.055f,.27f,.34f);
            string sceneFolder="Assets/MantisShrimpVR/Scenes";
            if(!AssetDatabase.IsValidFolder(sceneFolder))AssetDatabase.CreateFolder("Assets/MantisShrimpVR","Scenes");
            string dest=sceneFolder+"/MantisReef_V6.unity";
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),dest);
            AssetDatabase.SaveAssets();
            Debug.Log("MantisReef v6 saved to "+dest+". Extra decor: "+placed+", fish: "+fish+". VR setup remains manual; see README.");
            EditorUtility.DisplayDialog("v6 scene saved","Created "+dest+". Added "+fish+" fish and "+placed+" decorative assets. See README for Quest XR configuration.","OK");
        }
    }
}
