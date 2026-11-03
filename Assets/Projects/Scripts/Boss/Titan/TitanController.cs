using UnityEngine;

public class TitanController : BossController
{
    [SerializeField]
    private SpriteData Boost, Lunge, Attack, Tremor;
    private TitanMovement TM;
    private TitanCombat TC;//只有巨灵神使用怒气；其他Boss不依赖这个组件
    private ActorBoostTrail boostTrail;//复用四角色逐帧淡黑残影，不创建另一套表现逻辑
    private bool rageEffectsActive;//表现与属性独立清理，死亡冻结怒气数值时也能撤销无双效果
    [SerializeField, Min(0f)] private float skillCooldown = 10f;//原血量阈值强化的冷却；怒气结束后也重新计算
    private float nextSkillTime;//在冷却结束前不触发下一次血量阈值强化

    [SerializeField] private float boostRatio, boostDuration;
    [SerializeField] private float[] bloodRatio;//血量降低到对应阈值使用强化，可被打断，短暂无敌
    private int curBoostTime = 0;
    private float beginBoostTime = -1f;
    int skilled = 0;
    protected override void Awake()
    {
        base.Awake();
        TM = GetComponent<TitanMovement>();
        TC = GetComponent<TitanCombat>();
        boostTrail = GetComponent<ActorBoostTrail>();
    }
    protected override void OnEnable()
    {
        base.OnEnable();
        curBoostTime = 0;
        skilled = 0;
        beginBoostTime = -1f;
        nextSkillTime = 0f;
        rageEffectsActive = false;
        TM.SetRageBoost(false);
        if (attackedBox != null) attackedBox.gameObject.SetActive(true);
        if (TC != null) TC.RageBoostChanged += OnRageBoostChanged;
        if (EH != null) EH.Died += OnDied;
    }
    protected override void OnDisable()
    {
        if (TC != null) TC.RageBoostChanged -= OnRageBoostChanged;
        if (EH != null) EH.Died -= OnDied;
        ClearRageEffects();
        base.OnDisable();
    }
    protected override bool SkillBoost => skilled == 1;
    protected override void CheckSkillBoostLose()
    {
        if (SkillBoost && Time.time - beginBoostTime >= boostDuration)
        {
            skilled = 0;
            attackedBox.gameObject.SetActive(EC.Attack == -1);//只结束黄色强化的无敌，正在攻击时仍保留招式自己的无敌
            RefreshBoostVisual();
        }
    }
    protected override bool SkillReady => !EH.Boost && Time.time >= nextSkillTime && state != EnemyState.Skill &&
        curBoostTime < bloodRatio.Length && EH.ratio <= bloodRatio[curBoostTime];
    protected override void SkillSet()
    {
        curBoostTime++;
        nextSkillTime = Time.time + Mathf.Max(0f, skillCooldown);
    }
    protected override void HandleSkill() => SM.ChangeState(Boost);
    protected override void HandleAttack(int attack)
    {
        switch (attack)
        {
            case 0:SM.ChangeState(Attack);break;
            case 1:SM.ChangeState(Lunge);break;
            case 2:SM.ChangeState(Tremor);break;
        }
    }
    protected override void SkillOver()
    {
        EH.AttackedEnd();
        attackedBox.gameObject.SetActive(false);
        skilled = 1;
        beginBoostTime = Time.time;
        RefreshBoostVisual();
    }
    protected override void CheckSpecialAttack()
    {
        if (!TM.BeginLunge) TM.StartLunge();//每次冲撞单独开始计时，不能把默认-1当成已经冲撞超时
        if (TM.StopLunge)
        {
            AttackOver(true); return;
        }
        if (EH.Status != null && EH.Status.BlocksMovement)
        {
            TM.Stop();
            return;//融化阶段允许攻击动画与伤害，但冲撞不能突破冰块的位置锁
        }
        bool priR = TM.AttackFaceRight;
        bool r = TM.Lunging();
        SM.ChangeSpriteFace(r);
        EC.ChangeAttackBox(r);
        if (priR != r)
            EC.UpdateAttack();
    }
    protected override void SpecialAttackSet(float dis)
    {
        TM.AttackFaceRight = dis > 0f;
        attackedBox.gameObject.SetActive(false);//攻击过程无敌
    }
    protected override void SpecialAttackOver()
    {
        TM.LungeOver();
        attackedBox.gameObject.SetActive(!SkillBoost);//冲撞结束不能提前取消血量阈值强化的独立无敌
        TM.Stop();
    }
    protected override void SpecialOver()
    {
        if (SkillBoost)
            attackedBox.gameObject.SetActive(false);
    }

    private void OnRageBoostChanged(bool active)//只有专属怒气发出这个事件，通用Boss不自动拥有无双
    {
        if (EH == null || EH.Dead) return;
        rageEffectsActive = active;
        EH.SetSuperArmor(this, active);//普通霸体仍然扣血，不授予无敌或免疫冰冻等强控
        TM.SetRageBoost(active);
        if (boostTrail != null) boostTrail.SetActive(active);
        if (active)
        {
            if (state == EnemyState.Attacked && CanResetPose())
            {
                ReleaseControlPose();
                SetState(EnemyState.Idle);//已在播放的普通受击不阻挡刚获得的霸体行动
            }
        }
        else
        {
            AttackOver();//无双结束同时停止正在进行的攻击，避免新冷却中仍在持续冲撞
            nextSkillTime = Time.time + Mathf.Max(0f, skillCooldown);
            if (state == EnemyState.Skill)
            {
                skilled = 0;//尚未完成的强化动作结束，不在冷却期间补出技能效果
                beginBoostTime = -1f;
                attackedBox.gameObject.SetActive(true);
            }
            if (CanResetPose()) SetState(EnemyState.Idle);
        }
        RefreshBoostVisual();
    }

    private bool CanResetPose()//冰冻和携带仍有更高的控制权，不用切待机破坏定格姿势
        => (EH.Status == null || !EH.Status.BlocksActions) && !TM.IsCarried && !TM.IsStunned;

    private void RefreshBoostVisual()//黄色强化和怒气各自结束时，只撤销自己的表现来源
    {
        if (SkillBoost) SM.SetYellow();
        else if (rageEffectsActive) SM.SetRed();
        SM.SetAngry(SkillBoost || rageEffectsActive);
    }

    private void ClearRageEffects()//不修改怒气数值，死亡后UI继续保留最后一次的槽值
    {
        rageEffectsActive = false;
        if (EH != null) EH.SetSuperArmor(this, false);
        if (TM != null) TM.SetRageBoost(false);
        if (boostTrail != null) boostTrail.SetActive(false);
        if (SM != null) SM.SetAngry(false);
    }

    private void OnDied(DamageInfo damage) => ClearRageEffects();//死亡优先于无双剩余时间，立即结束残影与属性加成
}
