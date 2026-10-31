using System;
using UnityEngine;

public class SouthKingCombat : BossCombat
{
    public enum KingSkill { None = -1, IceSpikes, SwordFormation, IceShield, Counter }
    public enum CounterStage { None, Windup, Guard, Riposte, Recovery }
    [Header("招式调度")]
    [SerializeField] private SouthIceSpikes spikesPrefab;//剑插地后释放的独立冰锥波。
    [SerializeField] private SouthSwordFormation swordsPrefab;//第十一帧生成，目标位置在此刻锁定。
    [SerializeField] private SouthIceShield shieldPrefab;//独立耐久护盾，按受伤程度切换三种循环。
    [SerializeField] private float[] skillCooldowns = { 10f, 12f, 16f, 9f };//冰锥、剑阵、护盾、识破，从真正起手计时。
    [SerializeField, Min(0f)] private float firstSkillDelay = 3f, minimumAttackInterval = 0.75f;//首次技能准备与招式结束后的公共间隔。
    [SerializeField, Min(0)] private int spikesFrame = 8, swordFrame = 10;//下标从0开始，剑阵默认第十一帧。
    [SerializeField, Min(0f)] private float spikesOffset = 1.1f, swordHeight = 8f;//冰锥从脚前出现，剑阵从地面上方落下。
    [Header("识破")]
    [SerializeField, Min(0.01f)] private float counterWait = 1.2f;//第四帧持剑等待的时长。
    [SerializeField, Min(0f)] private float counterRange = 5f, counterDamage = 22f;//起手距离及识破反击的伤害。
    [SerializeField, Min(0)] private int counterHitFrame = 2;//反击片段（原第5至13帧）中的有效帧。
    [SerializeField] private Vector2 counterHitSize = new(5.5f, 4f);//反击向锁定方向伸展，本轮只命中一次。
    [Header("寒霜")]
    [SerializeField, Min(0.01f)] private float frostFull = 100f, frostDuration = 8f;//满槽保持至强化结束，死亡后槽定格。
    [SerializeField, Min(0f)] private float frostOnHit = 5f, frostOnMiss = 8f, frostOnDamaged = 10f;//沿用前两关专属状态的积累方式。
    [SerializeField, Range(0f, 1f)] private float normalFreezeChance = 0.1f, frostFreezeChance = 0.5f;//只影响普攻的冰冻概率。
    [SerializeField, Min(0f)] private float freezeDuration = 2f;//普通攻击触发后的完全冰冻时间。
    [SerializeField] private float frost;//当前槽值，由专属UI读取。
    private readonly float[] lastSkills = new float[4];//运行计时与预制体基础配置分开。
    private Health health;
    private SouthKingMovement movement;
    private PlayerManager players;
    private SouthIceShield shield;
    private float nextActionAt, guardUntil, frostUntil;
    private int nextSkill;
    private bool released, meleeActive, counterHit, deadFrozen, frostActive;
    public KingSkill Skill { get; private set; } = KingSkill.None;
    public CounterStage Counter { get; private set; }
    public float Frost => Mathf.Clamp(frost, 0f, MaxFrost);
    public float MaxFrost => Mathf.Max(0.01f, frostFull);
    public bool FrostActive => frostActive;
    public float NormalFreezeChance => Mathf.Clamp01(frostActive ? frostFreezeChance : normalFreezeChance);
    public float FreezeDuration => Mathf.Max(0f, freezeDuration);
    public PlayerController CurrentPlayer => players != null ? players.CurrentPlayer : null;
    public bool CanAct => BattleRun.IsRunning && isActiveAndEnabled && health != null && !health.Dead
        && (!health.Attacked || health.Boost) && !(health.Status != null && health.Status.BlocksActions)
        && movement != null && !movement.IsExternallyControlled;
    public bool CanStartAttack => CanAct && Skill == KingSkill.None && Time.time >= nextActionAt;
    public event Action FrostChanged;//槽和描边只在数据变化时更新。
    public event Action CounterRecognized;//扣血前识破后立即切动画，本次伤害不结算。

    protected override void Awake() { base.Awake(); ResolveReferences(); }
    private void ResolveReferences()
    {
        if (health == null) health = GetComponent<Health>();
        if (movement == null) movement = GetComponent<SouthKingMovement>();
        if (players == null)
            foreach (PlayerManager candidate in FindObjectsByType<PlayerManager>())
                if (candidate.isActiveAndEnabled) { players = candidate; break; }
    }
    protected override void OnEnable()
    {
        base.OnEnable(); ResolveReferences();
        Skill = KingSkill.None; Counter = CounterStage.None;
        released = meleeActive = counterHit = deadFrozen = frostActive = false;
        frost = frostUntil = 0f; nextSkill = 0;
        nextActionAt = Time.time + 1f;
        for (int i = 0; i < lastSkills.Length; i++) lastSkills[i] = Time.time - Cooldown(i) + Mathf.Max(0f, firstSkillDelay);
        if (health != null)
        {
            health.DamageBlocked += BlockDamage; health.DamageReceived += OnDamageReceived; health.Died += OnDied;
        }
        FrostChanged?.Invoke();
    }
    private float Cooldown(int index) => skillCooldowns != null && index >= 0 && index < skillCooldowns.Length
        ? Mathf.Max(0f, skillCooldowns[index]) : 10f;
    public KingSkill GetReadySkill()
    {
        ResolveReferences();
        if (!CanStartAttack || meleeActive || Attack >= 0) return KingSkill.None;
        for (int step = 0; step < 4; step++)
        {
            int index = (nextSkill + step) % 4;
            if (Time.time - lastSkills[index] < Cooldown(index)) continue;
            if (index == 0 && spikesPrefab == null || index == 1 && swordsPrefab == null
                || index == 2 && (shieldPrefab == null || shield != null && shield.BelongsTo(this) && shield.Protecting)
                || index == 3 && Mathf.Abs(movement.DeltaX) > Mathf.Max(0f, counterRange)) continue;
            return (KingSkill)index;
        }
        return KingSkill.None;
    }
    public bool BeginSkill(KingSkill skill, bool right)
    {
        if (skill == KingSkill.None || !CanStartAttack || meleeActive || Attack >= 0
            || Time.time - lastSkills[(int)skill] < Cooldown((int)skill)) return false;
        Skill = skill; Counter = skill == KingSkill.Counter ? CounterStage.Windup : CounterStage.None;
        released = counterHit = false; AttackFaceRight = right;
        lastSkills[(int)skill] = Time.time; nextSkill = ((int)skill + 1) % 4;
        health.Status?.NotifyAttackStarted(); return true;
    }
    public void BeginMelee() { meleeActive = true; }//基类已记录普攻冷却，只另外维护公共间隔。
    public void SampleMelee() => (CurrentAttackBox as SouthKingAttackBox)?.Sample(CurrentPlayer);
    public void CheckSkillFrame(int frame)
    {
        if (Skill == KingSkill.None || !CanAct) return;
        if (Skill == KingSkill.Counter)
        {
            if (Counter == CounterStage.Riposte && !counterHit && frame >= Mathf.Max(0, counterHitFrame)) HitCounter();
            return;
        }
        if (released || Skill == KingSkill.IceSpikes && frame < Mathf.Max(0, spikesFrame)
            || Skill == KingSkill.SwordFormation && frame < Mathf.Max(0, swordFrame)) return;
        released = true;//先锁门闩再生成，多次物理和渲染查询只发射一次。
        Vector2 position = transform.position;
        SouthKingEffectPool pool = SouthKingEffectPool.GetOrCreate();
        if (Skill == KingSkill.IceSpikes)
        {
            position.x += (AttackFaceRight ? 1f : -1f) * Mathf.Max(0f, spikesOffset);
            position.y = SouthKingEffect.GroundAt(position);
            pool.Fire(spikesPrefab, this, players, position, AttackFaceRight);
        }
        else if (Skill == KingSkill.SwordFormation)
        {
            if (CurrentPlayer != null) position.x = CurrentPlayer.transform.position.x;
            position.y = SouthKingEffect.GroundAt(position) + Mathf.Max(0f, swordHeight);
            pool.Fire(swordsPrefab, this, players, position, AttackFaceRight);
        }
        else if (Skill == KingSkill.IceShield)
            shield = pool.Fire(shieldPrefab, this, players, position, AttackFaceRight) as SouthIceShield;
    }
    public void EnterCounterGuard()
    {
        if (Skill != KingSkill.Counter || Counter != CounterStage.Windup || !CanAct) return;
        Counter = CounterStage.Guard; guardUntil = Time.time + Mathf.Max(0.01f, counterWait);
    }
    public bool GuardExpired => Counter == CounterStage.Guard && Time.time >= guardUntil;
    public void BeginCounterRecovery()
    {
        if (Skill != KingSkill.Counter) return;
        if (Counter == CounterStage.Riposte && !counterHit) HitCounter();//跨过有效帧时也不能漏掉已经完成的反击。
        Counter = CounterStage.Recovery; health?.RemoveInvincibility(this);
    }
    public void ProtectCounter(float duration) => health?.GrantInvincibility(this, Mathf.Max(0.1f, duration));
    private bool BlockDamage(DamageInfo damage)
    {
        if (health == null || health.Dead) return false;
        if (Skill == KingSkill.Counter && Counter == CounterStage.Guard && CanAct
            && Time.time < guardUntil && damage.Amount > 0f && !damage.IsPeriodic && !damage.IsReflected)
        {
            Counter = CounterStage.Riposte;
            float sourceX = damage.Source != null ? damage.Source.position.x - transform.position.x : movement.DeltaX;
            if (Mathf.Abs(sourceX) > 0.01f) AttackFaceRight = sourceX > 0f;//识破时锁定本次攻击来源，绕到背后的攻击也不会让反击挥空到旧方向。
            ProtectCounter(1f);//控制器按实际反击片段时长扩展，离开阶段时显式移除。
            AddFrost(frostOnDamaged); CounterRecognized?.Invoke();
            return true;//扣血、破冰和施加异常之前识破整次命中。
        }
        if (shield != null && shield.BelongsTo(this) && shield.Absorb(damage))
        {
            if (!damage.IsPeriodic) AddFrost(frostOnDamaged);
            return true;
        }
        return false;
    }
    private void HitCounter()
    {
        counterHit = true;
        PlayerController target = CurrentPlayer;
        Collider2D body = target != null ? target.GetComponent<Collider2D>() : null;
        Health targetHealth = target != null ? target.GetComponent<Health>() : null;
        Vector3 center = transform.position + Vector3.right * (AttackFaceRight ? 1f : -1f) * counterHitSize.x * 0.4f;
        Bounds area = new(center, new Vector3(Mathf.Max(0.1f, counterHitSize.x), Mathf.Max(0.1f, counterHitSize.y), 1f));
        bool hit = false;
        if (target != null && target.CanReceiveHit && body != null && targetHealth != null && !targetHealth.Dead
            && area.Intersects(body.bounds))
        {
            float blood = targetHealth.CurBlood;
            targetHealth.TakeDamage(new DamageInfo(transform, Mathf.Max(0f, counterDamage) * DamageMultiplier, true, kind: DamageKind.Skill));
            hit = targetHealth.CurBlood < blood;
        }
        AddFrost(hit ? frostOnHit : frostOnMiss);
    }
    public void CancelAction()//普通中断只取消待发效果，已离手的冰阵独立播放。
    {
        bool acted = meleeActive || Skill != KingSkill.None;
        meleeActive = released = false; Skill = KingSkill.None; Counter = CounterStage.None;
        health?.RemoveInvincibility(this);
        if (acted) nextActionAt = Time.time + Mathf.Max(0f, minimumAttackInterval);
    }
    public override void UpdateAngry() { }//关闭小怪自动怒气，避免与寒霜重复累计。
    protected override void OnAttackHit(int attack) => AddFrost(frostOnHit);
    protected override void OnAttackMissed(int attack) => AddFrost(frostOnMiss);
    public void ReportEffectResult(bool hit, int life)
    {
        if (health != null && health.LifeVersion == life && BattleRun.IsRunning) AddFrost(hit ? frostOnHit : frostOnMiss);
    }
    private void OnDamageReceived(DamageInfo damage, float actual)
    {
        if (actual <= 0f || damage.IsPeriodic) return;
        AddFrost(frostOnDamaged);
        if (!health.Boost) CancelAction();//保留受击请求，由共用控制器施加击退和切换动画。
    }
    private void AddFrost(float amount)
    {
        if (deadFrozen || frostActive || health == null || health.Dead || !isActiveAndEnabled || amount <= 0f) return;
        frost = Mathf.Min(MaxFrost, frost + amount);
        if (frost >= MaxFrost) { frostActive = true; frostUntil = Time.time + Mathf.Max(0.01f, frostDuration); }
        FrostChanged?.Invoke();
    }
    private void Update()
    {
        if (deadFrozen || health == null || health.Dead || !frostActive || Time.time < frostUntil) return;
        frostActive = false; frost = 0f; FrostChanged?.Invoke();
    }
    private void OnDied(DamageInfo damage)
    {
        deadFrozen = true; frostActive = false; CancelAction(); AttackEnd(); ClearAttackRequests();
        if (shield != null && shield.BelongsTo(this)) shield.StopEffect();
    }
    private void OnDisable()
    {
        if (health != null)
        {
            health.DamageBlocked -= BlockDamage; health.DamageReceived -= OnDamageReceived; health.Died -= OnDied;
        }
        if (shield != null && shield.BelongsTo(this)) shield.StopEffect();
        shield = null; frostActive = false; CancelAction();
    }
}
