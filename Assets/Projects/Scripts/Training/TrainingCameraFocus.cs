using UnityEngine;
using zmxy.GamePlay;

//训练场镜头：只负责开局把镜头放到“地面上方 4 单位”的基准高度，之后完全交给通用 CameraManager 跟随。
//必须早于 CameraManager.Awake 执行，CameraManager 会把此时的镜头高度记为基准高度；
//以前这里每帧再强行改一次镜头位置，与 CameraManager 互相拉扯，造成镜头和角色抖动。
[DefaultExecutionOrder(-500)]
public sealed class TrainingCameraFocus : MonoBehaviour
{
    [SerializeField, Min(0f)] private float heightAboveGround = 4f;

    private void Awake()
    {
        if (!TryGetGroundTop(out float groundTop)) return;
        Vector3 p = transform.position;
        p.y = groundTop + heightAboveGround;
        transform.position = p;
    }

    private void Start()
    {
        CameraManager cameraManager = GetComponent<CameraManager>();
        if (cameraManager != null) cameraManager.SetEdge(0);//训练场边界设在极远处，镜头可无限跟随
    }

    private static bool TryGetGroundTop(out float top)
    {
        TrainingLoopingEnvironment environment = FindAnyObjectByType<TrainingLoopingEnvironment>();
        BoxCollider2D ground = environment != null ? environment.GroundCollider : null;
        if (ground == null && environment != null) ground = environment.GetComponentInChildren<BoxCollider2D>();
        if (ground == null)
        {
            top = 0f;
            return false;
        }
        top = ground.bounds.max.y;
        return true;
    }
}
