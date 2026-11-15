using UnityEngine;
using UnityEngine.UI;

namespace MantisShrimpVR
{
    public class MantisNetChase : MonoBehaviour
    {
        public Transform player; public float speed=1.1f, triggerDistance=9f, stopDistance=1.7f;
        void Update(){if(!player)return;float d=Vector3.Distance(transform.position,player.position);if(d<triggerDistance&&d>stopDistance){Vector3 delta=player.position-transform.position;delta.y=0;transform.position+=delta.normalized*speed*Time.deltaTime;}}
    }
}
