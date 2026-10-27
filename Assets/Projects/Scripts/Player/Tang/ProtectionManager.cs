using UnityEngine;

public class ProtectionManager : MonoBehaviour
{
    [SerializeField] private SpriteManager SM;//仅播放护罩自己的帧动画，不影响唐僧本体动作
    [SerializeField] private SpriteData normal, highlight, warning;//普通四帧、精准抵挡四帧、结束提醒八帧
    [SerializeField, Min(0f)] private float perfectWindow = 0.2f;//开启后的免伤免控窗口，精准抵挡仅在这个时间内判定
    [SerializeField, Min(0f)] private float duration = 2f;//正常发光阶段的总时长，包含最开始的精准抵挡窗口
    [SerializeField, Min(0f)] private float warningDuration = 1f;//循环闪烁提醒的时长，到期后护罩完全结束
    [SerializeField, Range(0f, 1f)] private float cooldownReduction = 0.2f;//精准抵挡成功只扣除本次初始总冷却的这个比例
    private Health health;//免伤、短暂无敌与其他增益分别按来源登记
    private PlayerCombat combat;//仅在第一次精准抵挡后缩短技能一的冷却
    private SpriteRenderer sr;//隐藏护罩只关闭显示，不停用对象导致重复订阅
    private float openedAt, normalUntil, warningUntil;//使用游戏时间计时，受到控制不会延长护罩寿命
    private bool active, perfectBlocked, perfectWindowEnded;//一次释放最多奖励一次精准抵挡
    private enum ProtectionPhase { Hidden, Normal, Highlight, Warning }//动画阶段只在发生变化时切换
    private ProtectionPhase phase;

    private void Awake() => ResolveReferences();//允许初始停用的护罩在第一次开启时补齐自身引用

    private void ResolveReferences()//只查自身播放器和父角色组件，避免取到其他技能的播放器
    {
        if (SM == null) SM = GetComponent<SpriteManager>();
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (health == null) health = GetComponentInParent<Health>();
        if (combat == null) combat = GetComponentInParent<PlayerCombat>();
    }

    private void OnEnable()//初次随角色启用时保持隐藏，按键成功接受后才真正开启
    {
        ResolveReferences();
        active = false;
        phase = ProtectionPhase.Hidden;
        if (SM != null) SM.Pause();
        if (sr != null) sr.enabled = false;
        if (health != null)
        {
            health.InvincibleHit += OnInvincibleHit;
            health.Died += OnDied;
        }
    }

    public void OpenProtection()//技能一瞬发入口：本体继续当前动作，护罩独立计时
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        ResolveReferences();
        if (!isActiveAndEnabled || health == null || health.Dead) return;
        CloseProtection();//允许重新释放时清理上一轮自己的来源，不改变其他角色增益
        openedAt = Time.time;
        normalUntil = openedAt + Mathf.Max(duration, perfectWindow);
        warningUntil = normalUntil + warningDuration;
        perfectBlocked = perfectWindowEnded = false;
        active = true;
        health.SetDamageMultiplier(this, 0f);//后续受击仍经过命中反应，只把最终伤害降为零
        health.GrantInvincibility(this, perfectWindow);//仅初始窗口整次拒绝命中，因此伤害和控制都不会生效
        SetSortingOrder();
        if (sr != null) sr.enabled = true;
        ChangePhase(ProtectionPhase.Normal);
        if (warningUntil <= openedAt) CloseProtection();
    }

    private void SetSortingOrder()//罩始终显示在角色本体上方，不依赖关卡临时增加了多少显示顺序
    {
        Transform player = combat != null ? combat.transform : health.transform;
        SpriteRenderer visual = player.Find("Visual")?.GetComponent<SpriteRenderer>();
        if (visual == null) visual = player.GetComponent<SpriteRenderer>();
        if (visual == null || sr == null) return;
        sr.sortingLayerID = visual.sortingLayerID;
        sr.sortingOrder = visual.sortingOrder + 1;
    }

    private void OnInvincibleHit(DamageInfo damage)//普通攻击被初始窗口挡住时，只奖励一次高亮和冷却缩减
    {
        if (!active || perfectBlocked || perfectWindow <= 0f || Time.time >= openedAt + perfectWindow
            || damage.Amount <= 0f || damage.IsPeriodic || damage.IsReflected) return;
        perfectBlocked = true;
        if (combat != null) combat.ReduceSkillCooldown(1, cooldownReduction);
        ChangePhase(ProtectionPhase.Highlight);
    }

    private void Update()//动画持续时间独立于无敌窗口，高亮尚未播完也必须准时结束免控
    {
        if (!active) return;
        if (health == null || health.Dead || Time.time >= warningUntil)
        {
            CloseProtection();
            return;
        }
        if (!perfectWindowEnded && Time.time >= openedAt + perfectWindow)
        {
            perfectWindowEnded = true;
            health.RemoveInvincibility(this);//只撤销护罩的短暂无敌，保留换人等其他来源
        }
        if (Time.time >= normalUntil)
            ChangePhase(ProtectionPhase.Warning);
        else if (phase == ProtectionPhase.Highlight && (SM == null || SM.IsFinished || !HasSprites(highlight)))
            ChangePhase(ProtectionPhase.Normal);
    }

    private void ChangePhase(ProtectionPhase next)//反复检查当前阶段不会每帧重启精灵表
    {
        if (phase == next) return;
        phase = next;
        SpriteData data = next == ProtectionPhase.Highlight ? highlight : next == ProtectionPhase.Warning ? warning : normal;
        if (SM == null || !HasSprites(data)) return;
        SM.ChangeState(new SpriteData { sprites = data.sprites, fps = data.fps, loop = next != ProtectionPhase.Highlight });
    }

    private static bool HasSprites(SpriteData data) => data != null && data.sprites != null && data.sprites.Length > 0;//缺少高亮素材时安全退回普通阶段

    public void CloseProtection()//死亡、换人、对象停用和自然到期共用同一清理入口
    {
        active = false;
        phase = ProtectionPhase.Hidden;
        if (health != null)
        {
            health.RemoveInvincibility(this);
            health.RemoveDamageMultiplier(this);
        }
        if (SM != null) SM.Pause();
        if (sr != null) sr.enabled = false;
    }

    private void OnDied(DamageInfo damage) => CloseProtection();//死亡当帧结束自己的效果，不等待死亡动画

    private void OnDisable()//不允许换人后残留永久免伤或重复的事件订阅
    {
        CloseProtection();
        if (health == null) return;
        health.InvincibleHit -= OnInvincibleHit;
        health.Died -= OnDied;
    }
}
