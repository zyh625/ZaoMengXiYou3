using UnityEngine;
using zmxy.Legacy;
public class GameManager : MonoBehaviour
{

    public CameraManager cam;
    void Update()
    {
        if (cam != null) cam.UpdateBg();
    }
}
