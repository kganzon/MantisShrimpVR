using UnityEngine;
using UnityEngine.UI;

namespace MantisShrimpVR
{
    [RequireComponent(typeof(CharacterController))]
    public class MantisDesktopPlayer : MonoBehaviour
    {
        public Transform view; public Text narration; public MantisVision vision;
        public float speed=3f, mouseSensitivity=2f, hopSpeed=3.2f, ascentGravity=5f, descentGravity=1.7f, terminalFall=1.5f;
        public int maxAirBoosts=3; public float boostCooldown=.23f; public float maxHeight=12f;
        CharacterController cc; float vertical, pitch, lastBoost=-10f; int boosts; bool mouseLocked;
        void Start(){cc=GetComponent<CharacterController>(); if(!view && Camera.main)view=Camera.main.transform; SetLock(true);}
        void SetLock(bool value){mouseLocked=value;Cursor.lockState=value?CursorLockMode.Locked:CursorLockMode.None;Cursor.visible=!value;}
        void Update()
        {
            if(Input.GetKeyDown(KeyCode.Escape))SetLock(false);
            if(Input.GetMouseButtonDown(0) && !mouseLocked)SetLock(true);
            if(mouseLocked && view){transform.Rotate(0,Input.GetAxis("Mouse X")*mouseSensitivity,0);pitch=Mathf.Clamp(pitch-Input.GetAxis("Mouse Y")*mouseSensitivity,-80,80);view.localRotation=Quaternion.Euler(pitch,0,0);}
            if(Input.GetKeyDown(KeyCode.V))vision.Next();
            if(Input.GetKeyDown(KeyCode.Alpha1))vision.SetMode(0);
            if(Input.GetKeyDown(KeyCode.Alpha2))vision.SetMode(1);
            if(Input.GetKeyDown(KeyCode.Alpha3))vision.SetMode(2);
            if(cc.isGrounded){boosts=0;if(vertical<0)vertical=-.2f;}
            if(Input.GetKeyDown(KeyCode.Space) && Time.time-lastBoost>=boostCooldown && (cc.isGrounded || boosts<maxAirBoosts) && transform.position.y<maxHeight)
            {vertical=Mathf.Min(vertical+hopSpeed,6f);boosts++;lastBoost=Time.time;}
            vertical-= (vertical>0?ascentGravity:descentGravity)*Time.deltaTime;
            vertical=Mathf.Max(vertical,-terminalFall);
            Vector3 planar=transform.forward*Input.GetAxisRaw("Vertical")+transform.right*Input.GetAxisRaw("Horizontal");
            planar=Vector3.ClampMagnitude(planar,1)*speed;
            cc.Move((planar+Vector3.up*vertical)*Time.deltaTime);
            if(Input.GetMouseButtonDown(0) && mouseLocked) { MantisPrey.TryStrike(view,3.2f); }
        }
    }
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
    public class MantisNarration : MonoBehaviour
    {
        public Transform player, exitMarker, reflection; public Text text;
        bool exited, revealed;
        void Update()
        {
            if(!player || !text)return;
            if(!exited){text.text="A small creature awakens beneath the reef. Use WASD to swim toward the light. Space: buoyant hop.";
                if(exitMarker && Vector3.Distance(new Vector3(player.position.x,0,player.position.z),new Vector3(exitMarker.position.x,0,exitMarker.position.z))<2.5f)exited=true;}
            else if(!revealed){text.text="The reef hides unfamiliar patterns. Press V to change your senses. Find the shimmering shell.";
                if(reflection && Vector3.Distance(player.position,reflection.position)<4f)revealed=true;}
            else text.text="You are a peacock mantis shrimp! Click to strike prey. Avoid the approaching fishing net.";
        }
    }
    public class MantisReveal : MonoBehaviour
    {
        public Transform player; public GameObject shrimp; public float distance=4f;
        void Update(){if(!player||!shrimp)return; bool show=Vector3.Distance(player.position,transform.position)<distance;shrimp.SetActive(show);}
    }
    public class MantisPrey : MonoBehaviour
    {
        public static int score; public static Text scoreText;
        public static void TryStrike(Transform eye,float range)
        {
            if(!eye)return;
            RaycastHit[] hits=Physics.SphereCastAll(eye.position,.25f,eye.forward,range);
            foreach(var hit in hits){var prey=hit.collider.GetComponentInParent<MantisPrey>();if(prey){score+=10;if(scoreText)scoreText.text="FOOD + "+score;prey.gameObject.SetActive(false);break;}}
        }
    }
    public class MantisNetChase : MonoBehaviour
    {
        public Transform player; public float speed=1.1f, triggerDistance=9f, stopDistance=1.7f;
        void Update(){if(!player)return;float d=Vector3.Distance(transform.position,player.position);if(d<triggerDistance&&d>stopDistance){Vector3 delta=player.position-transform.position;delta.y=0;transform.position+=delta.normalized*speed*Time.deltaTime;}}
    }
    public class MantisFishSwim : MonoBehaviour
    {
        public float radius=1.6f, speed=.4f; Vector3 start;float phase;
        void Start(){start=transform.position;phase=Random.value*6.28f;}
        void Update(){float t=Time.time*speed+phase;transform.position=start+new Vector3(Mathf.Sin(t)*radius,Mathf.Sin(t*.6f)*.15f,Mathf.Cos(t)*radius);transform.rotation=Quaternion.LookRotation(new Vector3(Mathf.Cos(t),0,-Mathf.Sin(t)));}
    }
}
