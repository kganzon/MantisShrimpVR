using UnityEngine;
using UnityEngine.UI;

namespace MantisShrimpVR
{
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
}
