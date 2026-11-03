using UnityEngine;

//20000顺序在关卡终局判定和角色死亡处理之后提交拾取，避免同帧结算时多算一枚金币。
[DefaultExecutionOrder(20000)]
public class GoldCoin : MonoBehaviour
{
    [SerializeField] private SpriteManager SM;
    [SerializeField] private new SpriteData animation;//地面与吸附过程中都循环播放旋转帧
    [SerializeField, Min(0.01f)] private float existTime = 10f;//金币预制体自身的地面存在时长，所有关卡实例统一读取
    [SerializeField, Min(0.01f)] private float absorbTime = 0.45f;//金币预制体自身的完整吸附时长，完成并回收后才入账
    private SpriteRenderer sr;
    private GoldManager manager;
    private int amount;//这一枚表现对象所代表的实际金币数
    private float spawnedAt, lifetime, absorbDuration, absorbBeganAt;
    private Vector3 absorbStart;//从真正接触时的地面位置开始吸附，不瞬移到人物身上
    private bool running, absorbing;
    public int Amount => amount;

    private void Awake() => ResolveVisual();

    private void ResolveVisual()
    {
        if (SM == null) SM = GetComponent<SpriteManager>();
        if (sr == null) sr = GetComponent<SpriteRenderer>();
    }

    public void Spawn(GoldManager owner, Vector2 ground, int value)//管理器只传入本次掉落数据，动画与拾取过程使用预制体配置
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        ResolveVisual();
        manager = owner;
        amount = Mathf.Max(0, value);
        lifetime = Mathf.Max(0.01f, existTime);
        absorbDuration = Mathf.Max(0.01f, absorbTime);
        spawnedAt = Time.time;
        absorbing = false;
        running = true;
        transform.position = new Vector3(ground.x, ground.y, 0f);//金币帧采用底部居中pivot，放置点就是地面
        if (sr != null) sr.enabled = true;
        if (SM != null && animation != null && animation.sprites != null && animation.sprites.Length > 0)
            SM.ChangeState(new SpriteData { sprites = animation.sprites, fps = animation.fps, loop = true });
        UpdateSorting(manager != null ? manager.Collector : null);
    }

    private void LateUpdate()
    {
        if (!running) return;
        if (manager == null || !manager.CanCollect) { Cancel(); return; }
        PlayerController player = manager.Collector;
        if (player == null) { Cancel(); return; }
        UpdateSorting(player);
        if (!absorbing)
        {
            if (Time.time - spawnedAt >= lifetime) { Cancel(); return; }
            Collider2D body = player.GetComponent<BoxCollider2D>();//只认本体，攻击箱碰到金币不会触发拾取
            if (body == null || !body.enabled || sr == null) return;
            Bounds area = sr.bounds;
            Bounds bodyBounds = body.bounds;//2D接触只比较XY，人物为显示排序调整Z也不能导致金币捡不到
            if (area.max.x < bodyBounds.min.x || area.min.x > bodyBounds.max.x
                || area.max.y < bodyBounds.min.y || area.min.y > bodyBounds.max.y) return;
            absorbing = true;
            absorbBeganAt = Time.time;
            absorbStart = transform.position;
        }
        SpriteRenderer visual = player.BodySpriteManager != null ? player.BodySpriteManager.GetComponent<SpriteRenderer>() : null;
        Vector3 destination = visual != null ? visual.bounds.center : player.transform.position;
        if (sr != null) destination -= sr.bounds.center - transform.position;//底部pivot只用于落地，吸附终点必须让金币图片中心对齐角色图片中心
        destination.z = transform.position.z;
        float progress = Mathf.Clamp01((Time.time - absorbBeganAt) / absorbDuration);
        float eased = progress * progress * (3f - 2f * progress);//起步和收尾平滑，角色移动或切人时目标仍持续更新
        Vector3 position = Vector3.Lerp(absorbStart, destination, eased);
        position.y += Mathf.Sin(progress * Mathf.PI) * 0.2f;//从地面略微抬起后收向人物图片中心
        transform.position = position;
        if (progress >= 1f) manager.CompletePickup(this);
    }

    private void UpdateSorting(PlayerController player)
    {
        if (sr == null || player == null || player.BodySpriteManager == null) return;
        SpriteRenderer visual = player.BodySpriteManager.GetComponent<SpriteRenderer>();
        if (visual == null) return;
        sr.sortingLayerID = visual.sortingLayerID;
        sr.sortingOrder = visual.sortingOrder + 1;
    }

    public void Cancel()//到期、终局和成功收取都先回收表现；只有Manager成功入口会增加金币
    {
        running = absorbing = false;
        if (SM != null) SM.Pause();
        if (sr != null) sr.enabled = false;
        if (gameObject.activeSelf) gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        running = absorbing = false;
        if (SM != null) SM.Pause();
        if (sr != null) sr.enabled = false;
    }
}
