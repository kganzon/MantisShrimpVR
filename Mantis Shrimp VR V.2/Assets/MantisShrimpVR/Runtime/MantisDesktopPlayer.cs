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
}
