using UnityEngine;

public class PurpleRangedCombat : EnemyCombat
{
    [Header("射箭")]
    [SerializeField] private EnemyCheckAttack arrowCheck;//原预制体ArrowCheck，范围保持原样
    [SerializeField] private NormalArrowManager normalArrowPrefab;//普通箭模板，发射实例由场景对象池持有
    [SerializeField] private SpecialArrowManager specialArrowPrefab;//毒箭模板，与普通箭使用独立池
    [SerializeField, Min(0f)] private float shootCooldown = 3f;//两次开始开弓之间的最短间隔
    [SerializeField, Range(0f, 1f)] private float poisonArrowChance = 0.2f;//每次真正发射时判定一次毒箭概率
    [SerializeField, Min(0)] private int shootFrame = 9;//动作第十帧，下标从零开始
    [SerializeField] private Vector2 arrowSpawnOffset = new(1.8f, 0.2f);//相对根节点的发射位置，朝左只反转X
    [Header("虚化")]
    [SerializeField, Min(0f)] private float phaseCooldown = 10f;//上次攻击和上次虚化结束都经过此时长才可虚化
    [SerializeField, Min(0.01f)] private float phaseDuration = 6f;//虚化持续秒数，暂停时计时一同停止
    [SerializeField, Range(0f, 1f)] private float phaseAlpha = 0.5f;//只修改本体透明度，不关闭受击箱和状态效果
    private Health health;//通过攻击类别拒绝普通攻击，技能与周期状态继续结算
    private SpriteRenderer sr;//复用本体渲染器，不影响箭或异常状态的渲染器
    private BoxCollider2D checkBox;//直接核对玩家本体，攻击特效误入ArrowCheck不会触发射箭
    private readonly Collider2D[] checkResults = new Collider2D[32];//复用检测缓存，不逐帧分配数组
    private float lastShotTime, lastPhaseEnd, phaseUntil, originalAlpha;//对象池每次出生重新初始化计时
    private bool phaseActive, shotActive, arrowReleased, shotFaceRight;//同一轮动作只允许生成一支箭
    public bool ShotFaceRight => shotFaceRight;
    public bool IsPhased => phaseActive && Time.time < phaseUntil && health != null && !health.Dead;
    public float LastShotTime => lastShotTime;//供测试和后续敌人行为读取，不公开改写冷却
    public override float DamageMultiplier => base.DamageMultiplier;//箭与近战共用怒气及绿色音符的临时攻击增幅
    private float CurrentShootCooldown => Mathf.Max(0f, shootCooldown) * (Angry ? AngerCooldownMultiplier : 1f);
    public bool CanShoot => !shotActive && health != null && !health.Dead && !health.Attacked
        && !(health.Status != null && health.Status.BlocksActions)
        && Time.time - lastShotTime >= CurrentShootCooldown && PlayerInArrowCheck();

    protected override void Awake()
    {
        base.Awake();
        ResolveReferences();
        originalAlpha = sr != null ? sr.color.a : 1f;
    }
    private void ResolveReferences()//兼容组件Awake与OnEnable的执行顺序，预制体可显式填写ArrowCheck
    {
        if (health == null) health = GetComponent<Health>();
        if (sr == null) sr = transform.Find("Visual")?.GetComponent<SpriteRenderer>();
        if (sr == null) sr = GetComponentInChildren<SpriteRenderer>(true);
        if (arrowCheck == null)
            foreach (EnemyCheckAttack check in GetComponentsInChildren<EnemyCheckAttack>(true))
                if (check.name == "ArrowCheck") { arrowCheck = check; break; }
        if (checkBox == null && arrowCheck != null) checkBox = arrowCheck.GetComponent<BoxCollider2D>();
    }
    protected override void OnEnable()
    {
        base.OnEnable();
        ResolveReferences();
        phaseActive = shotActive = arrowReleased = false;
        lastShotTime = lastPhaseEnd = Time.time;//出生后先等待冷却，不在开局自动虚化
        if (health != null)
        {
            health.AttackIgnored += IgnoreAttack;
            health.Died += OnDied;
        }
        SetOpacity(false);
    }
    private bool IgnoreAttack(DamageKind kind) => isActiveAndEnabled && IsPhased && kind == DamageKind.NormalAttack;

    private void Update()//虚化与射箭动作分开计时，技能命中和普通眩晕不丢失虚化剩余时长
    {
        if (health == null || health.Dead) return;
        if (phaseActive)
        {
            if (Time.time < phaseUntil) return;
            phaseActive = false;
            lastPhaseEnd = Time.time;
            SetOpacity(false);
        }
        if (Time.time - lastShotTime < Mathf.Max(0f, phaseCooldown)
            || Time.time - lastPhaseEnd < Mathf.Max(0f, phaseCooldown)) return;
        phaseActive = true;
        phaseUntil = Time.time + Mathf.Max(0.01f, phaseDuration);
        SetOpacity(true);
    }
    private void SetOpacity(bool phased)//仅修改alpha，保留其他脚本设置的本体颜色
    {
        if (sr == null) return;
        Color color = sr.color;
        color.a = originalAlpha * (phased ? Mathf.Clamp01(phaseAlpha) : 1f);
        sr.color = color;
    }
    public void UpdateCheckFace(bool right)//保留原检测箱尺寸，只随当前目标翻转水平偏移
    {
        if (checkBox == null) return;
        Vector2 offset = checkBox.offset;
        offset.x = Mathf.Abs(offset.x) * (right ? 1f : -1f);
        checkBox.offset = offset;
    }
    private bool PlayerInArrowCheck()//只把玩家本体的碰撞箱算进射程，其他敌人和攻击箱不会触发
    {
        if (checkBox == null || !checkBox.enabled || !checkBox.gameObject.activeInHierarchy) return false;
        ContactFilter2D filter = ContactFilter2D.noFilter;
        int count = checkBox.Overlap(filter, checkResults);
        for (int i = 0; i < count; i++)
        {
            Collider2D hit = checkResults[i];
            PlayerController player = hit != null ? hit.GetComponentInParent<PlayerController>() : null;
            if (player == null || !player.isActiveAndEnabled || hit != player.GetComponent<Collider2D>()) continue;
            Health playerHealth = player.GetComponent<Health>();
            if (playerHealth != null && !playerHealth.Dead) return true;
        }
        return false;
    }
    public void BeginShot(bool right)//响应出招时立刻进入冷却，被打断也不跳过冷却
    {
        shotActive = true;
        arrowReleased = false;
        shotFaceRight = right;
        lastShotTime = Time.time;
        health?.Status?.NotifyAttackStarted();
    }
    public void CheckShot(int frame)//只在本轮有效动作达到第十帧后生成一次，受击或冻结立即取消待发射请求
    {
        if (!shotActive || arrowReleased) return;
        if (!isActiveAndEnabled || health == null || health.Dead || health.Attacked
            || health.Status != null && health.Status.BlocksActions)
        {
            CancelShot();
            return;
        }
        if (frame < shootFrame) return;
        arrowReleased = true;
        NormalArrowManager prefab = specialArrowPrefab != null && Random.value < Mathf.Clamp01(poisonArrowChance)
            ? specialArrowPrefab : normalArrowPrefab;
        if (prefab == null) return;
        Vector2 position = (Vector2)transform.position + new Vector2(
            shotFaceRight ? arrowSpawnOffset.x : -arrowSpawnOffset.x, arrowSpawnOffset.y);
        EnemyProjectileManager.GetOrCreate().Fire(prefab, this, position, shotFaceRight);
    }
    public void CancelShot() => shotActive = false;//只取消尚未生成的箭，已经飞出的实例由对象池继续管理
    private void OnDied(DamageInfo damage)
    {
        CancelShot();
        phaseActive = false;
        SetOpacity(false);
    }
    private void OnDisable()
    {
        if (health != null)
        {
            health.AttackIgnored -= IgnoreAttack;
            health.Died -= OnDied;
        }
        CancelShot();
        phaseActive = false;
        SetOpacity(false);
    }
}
