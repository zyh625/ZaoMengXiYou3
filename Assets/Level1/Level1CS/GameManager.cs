using UnityEngine;
using zmxy.Legacy;
public class GameManager : MonoBehaviour
{

    public Player player;
    public CameraManager cam;
    void Update()
    {
        player.UpdateMonkey();
        cam.UpdateBg();
    }
}
