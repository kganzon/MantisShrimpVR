using UnityEngine;
using UnityEngine.UI;

namespace MantisShrimpVR
{
    public class MantisFishSwim : MonoBehaviour
    {
        public float radius=1.6f, speed=.4f; Vector3 start;float phase;
        void Start(){start=transform.position;phase=Random.value*6.28f;}
        void Update(){float t=Time.time*speed+phase;transform.position=start+new Vector3(Mathf.Sin(t)*radius,Mathf.Sin(t*.6f)*.15f,Mathf.Cos(t)*radius);transform.rotation=Quaternion.LookRotation(new Vector3(Mathf.Cos(t),0,-Mathf.Sin(t)));}
    }
}
