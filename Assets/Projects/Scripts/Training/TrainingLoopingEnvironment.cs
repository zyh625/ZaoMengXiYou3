using UnityEngine;

/// <summary>
/// 训练场无缝循环背景与地面管理系统：
/// 尊重用户在场景中调节的地板、天空与碰撞体高度；
/// 运行时仅在水平方向根据摄像机中心平滑循环平铺，并让地面碰撞体水平跟随摄像机，
/// 绝不篡改用户在编辑器中配置的碰撞体高度 (Y)、偏移 (Offset) 或尺寸 (Size)。
/// </summary>
public sealed class TrainingLoopingEnvironment : MonoBehaviour
{
    [Header("Sky Blocks (Order: Left, Center, Right)")]
    [SerializeField] private Transform[] skyBlocks;

    [Header("Ground Blocks")]
    [SerializeField] private Transform[] groundBlocks;

    [Header("Ground Collider")]
    [SerializeField] private BoxCollider2D groundCollider;

    [Header("Settings")]
    [SerializeField] private float skyUnitWidth = 40.182f;
    [SerializeField] private float groundUnitWidth = 17.74f;

    private Camera targetCamera;

    public BoxCollider2D GroundCollider => groundCollider;

    /// <summary>
    /// 获取当前物理地面的最高走道表面世界高度（Y）。
    /// </summary>
    public float GroundTop
    {
        get
        {
            if (groundCollider != null)
            {
                return groundCollider.bounds.max.y;
            }
            return -2.8f;
        }
    }

    private void Awake()
    {
        targetCamera = Camera.main;

        // 如果未在 Inspector 显式指定，则自动从子物体中检索
        if (skyBlocks == null || skyBlocks.Length == 0)
        {
            var skys = transform.Find("SkyGroup");
            if (skys != null)
            {
                skyBlocks = new Transform[skys.childCount];
                for (int i = 0; i < skys.childCount; i++) skyBlocks[i] = skys.GetChild(i);
            }
        }

        if (groundBlocks == null || groundBlocks.Length == 0)
        {
            var grounds = transform.Find("GroundGroup");
            if (grounds != null)
            {
                groundBlocks = new Transform[grounds.childCount];
                for (int i = 0; i < grounds.childCount; i++) groundBlocks[i] = grounds.GetChild(i);
            }
        }

        if (groundCollider == null)
        {
            groundCollider = GetComponentInChildren<BoxCollider2D>();
        }

        // 确保物理层级为 Ground 层，绝不改写用户的 offset 和 size！
        if (groundCollider != null)
        {
            groundCollider.gameObject.layer = 6; // Layer 6: Ground
        }
    }

    private void Start()
    {
        if (targetCamera == null) targetCamera = Camera.main;
    }

    private void LateUpdate()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
            if (targetCamera == null) return;
        }

        float camX = targetCamera.transform.position.x;

        // 1. 无缝循环平铺背景（仅修改 X 坐标，尊重用户的 Y 高度设置）
        if (skyBlocks != null && skyBlocks.Length > 0)
        {
            float totalSkySpan = skyUnitWidth * skyBlocks.Length;
            float halfSky = skyUnitWidth * (skyBlocks.Length * 0.5f);
            for (int i = 0; i < skyBlocks.Length; i++)
            {
                if (skyBlocks[i] == null) continue;
                while (camX - skyBlocks[i].position.x > halfSky)
                {
                    skyBlocks[i].position += Vector3.right * totalSkySpan;
                }
                while (skyBlocks[i].position.x - camX > halfSky)
                {
                    skyBlocks[i].position -= Vector3.right * totalSkySpan;
                }
            }
        }

        // 2. 无缝循环平铺地面（仅修改 X 坐标，尊重用户的 Y 高度设置）
        if (groundBlocks != null && groundBlocks.Length > 0)
        {
            float totalGroundSpan = groundUnitWidth * groundBlocks.Length;
            float halfGround = groundUnitWidth * (groundBlocks.Length * 0.5f);
            for (int i = 0; i < groundBlocks.Length; i++)
            {
                if (groundBlocks[i] == null) continue;
                while (camX - groundBlocks[i].position.x > halfGround)
                {
                    groundBlocks[i].position += Vector3.right * totalGroundSpan;
                }
                while (groundBlocks[i].position.x - camX > halfGround)
                {
                    groundBlocks[i].position -= Vector3.right * totalGroundSpan;
                }
            }
        }

        // 3. 地面碰撞体仅水平跟随摄像机，其 Y 坐标由用户在 Inspector 中的摆放严格决定！
        if (groundCollider != null)
        {
            Vector3 p = groundCollider.transform.position;
            p.x = camX;
            groundCollider.transform.position = p;
        }
    }
}
