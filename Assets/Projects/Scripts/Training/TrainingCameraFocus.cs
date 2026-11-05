using UnityEngine;
using zmxy.GamePlay;

[DefaultExecutionOrder(2000)]
public sealed class TrainingCameraFocus : MonoBehaviour
{
    [SerializeField] private TrainingManager manager;
    private CameraManager cameraManager;
    private TrainingLoopingEnvironment loopingEnv;
    private float cachedGroundTop = -3.07f;
    private bool hasCachedGround = false;

    private void Awake()
    {
        cameraManager = GetComponent<CameraManager>();
    }

    private void Start()
    {
        // 训练场中移除相机边缘限制，允许玩家无限移动
        if (cameraManager != null)
        {
            cameraManager.SetEdge(0);
        }

        // 启动时立即定位到地面黄金高度，绝不跟随角色下坠
        InitCameraHeight();
    }

    private float GetGroundTop()
    {
        if (loopingEnv == null)
        {
            loopingEnv = FindAnyObjectByType<TrainingLoopingEnvironment>();
        }
        if (loopingEnv != null)
        {
            cachedGroundTop = loopingEnv.GroundTop;
            hasCachedGround = true;
            return cachedGroundTop;
        }
        if (!hasCachedGround)
        {
            BoxCollider2D[] colliders = FindObjectsByType<BoxCollider2D>(FindObjectsInactive.Exclude);
            foreach (var col in colliders)
            {
                if (col.gameObject.layer == 6) // Layer 6: Ground
                {
                    cachedGroundTop = col.bounds.max.y;
                    hasCachedGround = true;
                    return cachedGroundTop;
                }
            }
        }
        return cachedGroundTop;
    }

    private void InitCameraHeight()
    {
        float groundTop = GetGroundTop();
        // 基准高度：地面之上 4.0 单位（与正式关卡 CameraManager 的黄金视野完全一致）
        float baseY = groundTop + 4.0f;
        Vector3 p = transform.position;
        p.y = baseY;
        transform.position = p;
    }

    private void LateUpdate()
    {
        if (!TrainingSession.Active)
            return;

        if (manager == null) manager = FindAnyObjectByType<TrainingManager>();
        if (manager == null || !manager.Ready || manager.Players == null || manager.Players.CurrentPlayer == null)
            return;

        Transform player = manager.Players.CurrentPlayer.transform;
        Vector3 p = transform.position;

        // 1. 水平方向严格锁定角色中心 X：玩家左右移动没有任何死锁与限制，镜头中心始终跟随！
        p.x = player.position.x;

        // 2. 垂直方向稳固基准高度机制：
        // 镜头绝不跟随角色自由下坠！基准高度 baseY 锚定在地面上方 4.0 单位处，
        // 角色站立在地面或跳跃落地时，镜头稳定停留在 baseY，绝不往下跌落！
        float groundTop = GetGroundTop();
        float baseY = groundTop + 4.0f;
        float targetY = baseY;

        // 仅当玩家跳跃高出镜头中心（起跳高度大于 baseY + 0.35f）时，镜头才平滑向上跟起
        float triggerY = baseY + 0.35f;
        if (player.position.y > triggerY)
        {
            targetY = Mathf.Clamp(player.position.y, baseY, baseY + 4.0f);
        }

        // 向上平滑跟随速度为 6，落地平缓回落速度为 4
        if (p.y < targetY)
        {
            p.y = Mathf.MoveTowards(p.y, targetY, 6f * Time.deltaTime);
        }
        else if (p.y > targetY)
        {
            p.y = Mathf.MoveTowards(p.y, targetY, 4f * Time.deltaTime);
        }

        // 强制底线保护：垂直高度绝不能跌破 baseY，彻底杜绝“镜头跟着往下掉”！
        if (p.y < baseY)
        {
            p.y = baseY;
        }

        transform.position = p;
    }
}
