using UnityEngine;
using UnityEngine.UI;

namespace MantisShrimpVR
{
    public class MantisReveal : MonoBehaviour
    {
        public Transform player; public GameObject shrimp; public float distance=4f;
        void Update(){if(!player||!shrimp)return; bool show=Vector3.Distance(player.position,transform.position)<distance;shrimp.SetActive(show);}
    }
}
