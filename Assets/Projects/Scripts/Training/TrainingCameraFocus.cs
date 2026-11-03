using UnityEngine;
using zmxy.GamePlay;

[DefaultExecutionOrder(2000)]
public sealed class TrainingCameraFocus : MonoBehaviour
{
    [SerializeField] private TrainingManager manager;
    private CameraManager cameraManager;
    private void Awake()=>cameraManager=GetComponent<CameraManager>();
    private void LateUpdate()
    {
        if(!TrainingSession.Active || !manager.Ready || manager.Players.CurrentPlayer==null)return;
        Transform player=manager.Players.CurrentPlayer.transform;float x=manager.Target!=null?(player.position.x+manager.Target.transform.position.x)*0.5f:player.position.x;
        Vector3 p=transform.position;p.x=Mathf.Clamp(x,cameraManager.EdgeL,cameraManager.EdgeR);p.y=Mathf.Max(2.2f,player.position.y-1f);transform.position=p;
    }
}
