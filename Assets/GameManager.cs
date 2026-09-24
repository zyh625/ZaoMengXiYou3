using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Unity.VisualScripting;
using UnityEditor.PackageManager;
[System.Serializable]
public class AnimationData
{
    public Sprite[] frames;
}
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
