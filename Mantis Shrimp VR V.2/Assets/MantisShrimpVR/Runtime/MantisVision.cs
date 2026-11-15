using UnityEngine;
using UnityEngine.UI;

namespace MantisShrimpVR
{
    public class MantisVision : MonoBehaviour
    {
        public Camera target; public Text label; int mode;
        public void Next(){SetMode((mode+1)%3);}
        public void SetMode(int m)
        {
            mode=Mathf.Clamp(m,0,2); if(!target)target=Camera.main;
            Color fog=mode==0?new Color(.045f,.27f,.35f):mode==1?new Color(.16f,.14f,.42f):new Color(.30f,.22f,.38f);
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogDensity=.026f;RenderSettings.fogColor=fog;
            RenderSettings.ambientLight=mode==0?new Color(.48f,.63f,.65f):mode==1?new Color(.52f,.48f,.76f):new Color(.73f,.55f,.72f);
            if(target){target.clearFlags=CameraClearFlags.SolidColor;target.backgroundColor=fog;}
            if(label)label.text=new[]{"1 NATURAL","2 UV-INSPIRED","3 POLARIZATION-INSPIRED"}[mode];
        }
        void Start(){SetMode(0);}
    }
}
