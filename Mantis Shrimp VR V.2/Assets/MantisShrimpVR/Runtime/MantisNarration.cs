using UnityEngine;
using UnityEngine.UI;

namespace MantisShrimpVR
{
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
}
