using System.Collections.Generic;
using UnityEngine;

//状态表现只负责跟随、尺寸与帧动画；通用对象池让所有玩家和敌人复用同一组预制体。
[DefaultExecutionOrder(10010)]
public class StatusEffectView : MonoBehaviour
{
    [SerializeField] private SpriteManager SM;
    [SerializeField] private SpriteData loop, breaking, melting;//冰冻循环/破裂/融化，毒雾只使用loop
    [SerializeField] private Vector2 referenceSize;//循环形态真实像素范围的本地尺寸，不用包含飞散碎片的整张裁框缩放
    [SerializeField] private Vector2 sizeRatio = Vector2.one;//相对本体帧图片的覆盖宽高；毒雾可只覆盖胸口
    [SerializeField] private Vector2 anchorOffset;//按本体帧宽高计算的中心偏移
    [SerializeField] private bool anchorToBodyCollider;//毒雾以身体碰撞箱定位胸口，避免挥动长兵器时跟着图片外框中心漂走
    [SerializeField] private int sortingOffset = 2;//状态覆盖本体及普通技能，跟随目标排序层
    private static readonly Dictionary<StatusEffectView, Stack<StatusEffectView>> pools = new();
    private static Transform poolRoot;//放在场景共同根节点下，不跟随角色停用而丢失缓存
    private StatusEffectView prefab;//归还时保留它来自哪个公共预制体
    private Transform target;
    private SpriteRenderer bodyRenderer, sr;
    private Collider2D bodyCollider;//胸口表现只在绑定目标时查找一次身体碰撞箱
    private bool returned, looping, breakingNow;//Release幂等，破裂动画结束后自动归还
    public int MeltingFrameCount => melting != null && melting.sprites != null ? melting.sprites.Length : 6;

    private void Awake() => ResolveVisual();

    private void ResolveVisual()
    {
        if (SM == null) SM = GetComponent<SpriteManager>();
        if (sr == null) sr = GetComponent<SpriteRenderer>();
    }

    public static StatusEffectView Rent(StatusEffectView source, Transform owner, SpriteRenderer visual)
    {
        if (source == null || owner == null) return null;
        if (poolRoot == null) poolRoot = new GameObject("StatusEffects").transform;
        if (!pools.TryGetValue(source, out Stack<StatusEffectView> available))
            pools[source] = available = new Stack<StatusEffectView>();
        StatusEffectView view = null;
        while (available.Count > 0 && view == null) view = available.Pop();
        if (view == null) view = Instantiate(source, poolRoot);
        view.prefab = source;
        view.target = owner;
        view.bodyRenderer = visual;
        view.bodyCollider = owner.GetComponent<Collider2D>();
        view.returned = view.looping = view.breakingNow = false;
        view.gameObject.SetActive(true);
        view.ResolveVisual();
        view.FollowTarget();
        return view;
    }

    public void PlayLoop()
    {
        if (returned || looping) return;
        looping = true;
        breakingNow = false;
        if (sr != null) sr.enabled = true;
        if (SM != null && HasSprites(loop))
            SM.ChangeState(new SpriteData { sprites = loop.sprites, fps = loop.fps, loop = true });
    }

    public void PlayBreaking()//伤害已经由Health结算，表现不能在动画结束时重复造成伤害
    {
        if (returned) return;
        looping = false;
        breakingNow = true;
        if (SM == null || !HasSprites(breaking)) { Release(); return; }
        if (sr != null) sr.enabled = true;
        SM.ChangeState(new SpriteData { sprites = breaking.sprites, fps = breaking.fps, loop = false });
    }

    public void ShowMeltingFrame(int frame)//融化帧完全由状态推进，主动攻击和自然计时共用相同索引
    {
        if (returned) return;
        looping = breakingNow = false;
        if (SM != null) SM.Pause();
        if (sr == null || !HasSprites(melting)) return;
        sr.sprite = melting.sprites[Mathf.Clamp(frame, 0, melting.sprites.Length - 1)];
        sr.enabled = true;
    }

    private void LateUpdate()
    {
        if (returned) return;
        if (target == null || !target.gameObject.activeInHierarchy) { Release(); return; }
        FollowTarget();
        if (breakingNow && (SM == null || SM.IsFinished)) Release();
    }

    private void FollowTarget()//每帧使用当前本体图片范围，切Sprite和变身后仍能完整包住角色
    {
        if (target == null || sr == null) return;
        Bounds bounds = bodyRenderer != null ? bodyRenderer.bounds : new Bounds(target.position, Vector3.one);
        Vector3 position = anchorToBodyCollider && bodyCollider != null ? bodyCollider.bounds.center : bounds.center;
        position.x += bounds.size.x * anchorOffset.x;
        position.y += bounds.size.y * anchorOffset.y;
        position.z = target.position.z;
        transform.position = position;
        Vector2 reference = referenceSize;
        if ((reference.x <= 0f || reference.y <= 0f) && HasSprites(loop)) reference = loop.sprites[0].bounds.size;
        Vector2 size = new(bounds.size.x * sizeRatio.x, bounds.size.y * sizeRatio.y);
        if (anchorToBodyCollider && bodyCollider != null)//攻击拖影变宽时毒雾仍只笼罩胸口，不随武器范围放大到全屏
        {
            size.x = Mathf.Min(size.x, bodyCollider.bounds.size.x * 1.5f);
            size.y = Mathf.Min(size.y, bodyCollider.bounds.size.y * 0.6f);
        }
        transform.localScale = new Vector3(size.x / Mathf.Max(0.01f, reference.x), size.y / Mathf.Max(0.01f, reference.y), 1f);
        if (bodyRenderer == null) return;
        sr.sortingLayerID = bodyRenderer.sortingLayerID;
        sr.sortingOrder = bodyRenderer.sortingOrder + sortingOffset;
    }

    public void Release()
    {
        if (returned) return;
        returned = true;
        looping = breakingNow = false;
        target = null;
        bodyRenderer = null;
        bodyCollider = null;
        if (SM != null) SM.Pause();
        if (sr != null) sr.enabled = false;
        gameObject.SetActive(false);
        if (prefab == null) return;
        if (!pools.TryGetValue(prefab, out Stack<StatusEffectView> available))
            pools[prefab] = available = new Stack<StatusEffectView>();
        available.Push(this);
    }

    private static bool HasSprites(SpriteData data) => data != null && data.sprites != null && data.sprites.Length > 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetPool()//关闭Domain Reload时重新进游戏也不能沿用上次场景已销毁的池根
    {
        pools.Clear();
        poolRoot = null;
    }
}
