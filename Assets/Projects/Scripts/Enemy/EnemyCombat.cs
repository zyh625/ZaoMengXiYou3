
using System;
using System.Collections.Generic;
using UnityEngine;

public class EnemyCombat : MonoBehaviour
{
    [SerializeField] private BoxCollider2D[] AttackBoxes;//攻击的碰撞箱
    private EnemyAttackBox[] enemyAttackBoxes;
    [SerializeField] private float[] attackDelay;//攻击冷却
    private float[] curAttackDelay;//当前攻击冷却
    [SerializeField] private EnemyCheckAttack[] CheckAttackLogic;//攻击的检查逻辑
    private BoxCollider2D[] CheckAttackBoxes;//攻击的检查箱
    [SerializeField] private int[] AttackOpenIdx, AttackShutIdx;
    public bool AttackOpen(int idx) => idx >= AttackOpenIdx[curAttack] 
        && idx < AttackShutIdx[curAttack];
    public bool AttackShut(int idx) => idx >= AttackShutIdx[curAttack];
    private float[] lastAttack;
    public bool[] AttackRequest;
    private int attackNum;
    private int curAttack = -1;
    public int Attack => curAttack;
    private bool attackWindowOpened, attackHitReported;//一整招只结算一次命中或落空，重复碰撞和冲刺转向不重复累计
    private float externalDamageScale = 1f;//关卡召唤等外部来源的伤害倍率；池成员复用时由生成代码显式恢复为1
    public void SetExternalDamageScale(float value) => externalDamageScale = Mathf.Clamp(value, 0f, 10f);//不修改预制体基础攻击
    public virtual float DamageMultiplier => (Angry ? Mathf.Max(0f, angryRatio) : 1f) * AttackBuffMultiplier * externalDamageScale;//怒气、临时增幅与外部倍率分别计算
    private readonly Dictionary<UnityEngine.Object, TimedAttackBuff> attackBuffs = new();//同来源刷新，不同来源的攻击增幅独立到期
    private readonly List<UnityEngine.Object> expiredAttackBuffs = new();//复用过期来源列表，命中查询不额外分配集合
    private readonly struct TimedAttackBuff//仅保存本次生命内的临时增益，不写入存档或基础数值
    {
        public readonly float Multiplier, Until;
        public TimedAttackBuff(float multiplier, float until) { Multiplier = multiplier; Until = until; }
    }
    protected float AttackBuffMultiplier//所有近战攻击箱及远程发射快照共用倍率入口
    {
        get
        {
            float multiplier = 1f;
            expiredAttackBuffs.Clear();
            foreach (var entry in attackBuffs)
            {
                if (entry.Key == null || Time.time >= entry.Value.Until) expiredAttackBuffs.Add(entry.Key);
                else multiplier *= entry.Value.Multiplier;
            }
            foreach (var source in expiredAttackBuffs) attackBuffs.Remove(source);
            return multiplier;
        }
    }
    public void ApplyAttackBuff(UnityEngine.Object source, float multiplier, float duration)//绿色音符等外部增益按来源刷新持续时间
    {
        if (source == null || !isActiveAndEnabled || duration <= 0f || float.IsNaN(duration) || float.IsInfinity(duration)
            || multiplier < 0f || float.IsNaN(multiplier) || float.IsInfinity(multiplier)) return;
        Health health = GetComponent<Health>();
        if (health == null || health.Dead) return;
        attackBuffs[source] = new TimedAttackBuff(multiplier, Time.time + duration);
    }
    public void RemoveAttackBuff(UnityEngine.Object source)//只移除自己提供的增益，保留其他来源
    {
        if (source != null) attackBuffs.Remove(source);
    }
    protected virtual bool UsesAnger => true;//普通小怪保留原怒气；Boss基类默认关闭，具体Boss自行扩展特殊状态
    public void AttackOver(bool completed = false)//正常播完才结算落空，受击或控制打断不当成玩家躲避
    {
        if (curAttack < 0) return;
        int finishedAttack = curAttack;
        bool missed = completed && attackWindowOpened && !attackHitReported;
        curAttack = -1;
        attackWindowOpened = attackHitReported = false;
        if (missed) OnAttackMissed(finishedAttack);
    }
    protected virtual void OnAttackHit(int attack) { }//子类可记录完整一招的首次有效命中
    protected virtual void OnAttackMissed(int attack) { }//子类可记录有攻击窗口却完整落空的一招
    [SerializeField] private float angryFull, angryRatio,deltaAngry,angryDuration,delayRatio;//怒气总量，愤怒状态下的攻击增益
    [SerializeField]private float curAngry;
    private bool angry = false;
    private ActorStatus status;//融化只在真正开始一招时推进，不把碰撞检测或按帧伤害当成额外攻击
    public bool Angry => UsesAnger && angry;
    protected float AngerAttackMultiplier => Mathf.Max(0f, angryRatio);//远程攻击也能沿用小怪原有愤怒配置
    protected float AngerCooldownMultiplier => Mathf.Max(0f, delayRatio);
    public bool AngryRequest = false;
    private float lastAngry = -1f;
    public bool AngryEnd=> UsesAnger && (Time.time - lastAngry) >= angryDuration;
    public bool AttackReady()//是否能攻击
    {
        for(int i=0;i<attackNum;i++)
            if (AttackRequest[i])return true;
        return false;
    }
    public bool AttackFaceRight;
    protected virtual void Awake()
    {
        status = GetComponent<Health>()?.Status;
        attackNum = AttackBoxes.Length;
        lastAttack = new float[attackNum];
        AttackRequest = new bool[attackNum];
        curAttackDelay = new float[attackNum];
        CheckAttackBoxes=new BoxCollider2D[attackNum];
        enemyAttackBoxes=new EnemyAttackBox[attackNum];
        for (int i = 0; i < attackNum; i++)
        {
            CheckAttackBoxes[i] = CheckAttackLogic[i].GetComponent<BoxCollider2D>();
            enemyAttackBoxes[i] = AttackBoxes[i].GetComponent<EnemyAttackBox>();
            curAttackDelay[i] = attackDelay[i];
        }
    }
    protected virtual void OnEnable()
    {
        attackBuffs.Clear();//对象池下次重生不继承上一条生命获得的增幅
        angry = false;
        curAttack = -1;
        attackWindowOpened = attackHitReported = false;
        if (!UsesAnger) AngryRequest = false;
        for(int i = 0; i < attackNum; i++)
        {
            AttackBoxes[i].gameObject.SetActive(false);
            enemyAttackBoxes[i].ReturnBegin();
            curAttackDelay[i] = attackDelay[i];
            lastAttack[i] = Time.time;//开始都进入冷却，给玩家反应时间
            AttackRequest[i] = false;
        }
    }
    public void CheckAttack()//检查当前是否满足攻击条件
    {
        if (status != null && status.BlocksActions)
        {
            ClearAttackRequests();
            return;
        }
        if (Attack != -1) return;
        for (int i = 0; i < attackNum; i++)
            AttackRequest[i] = CheckAttackLogic[i].PlayerIn &&
            Time.time - lastAttack[i] >= curAttackDelay[i];
    }
    public void HasAtatck(bool r)//成功攻击
    {
        if (status != null && status.BlocksActions) return;
        int cur = -1;
        for(int i=0; i<attackNum; i++)
            if (AttackRequest[i])
            {
                cur = i;break;
            }
        if (cur < 0) return;//不存在有效请求时不提交招式，也不消耗融化帧
        curAttack = cur;
        attackWindowOpened = attackHitReported = false;
        lastAttack[cur] = Time.time;
        AttackFaceRight = r;
        ChangeBoxFace(AttackBoxes[cur], r);
        status?.NotifyAttackStarted();
    }
    public void ClearAttackRequests()//冰冻中丢弃尚未开始的旧攻击请求，融化后重新按实际距离判断
    {
        if (AttackRequest != null) Array.Clear(AttackRequest, 0, AttackRequest.Length);
    }
    public void AttackBegin()//开始有效攻击
    {
        if (curAttack < 0) return;
        attackWindowOpened = true;
        if (!AttackBoxes[curAttack].gameObject.activeSelf)
            AttackBoxes[curAttack].gameObject.SetActive(true);
    }
    public void ReportAttackHit(EnemyAttackBox attackBox)//实际扣血后才确认命中，无敌挡住或同一招重复命中不重复通知
    {
        if (curAttack < 0 || attackHitReported || enemyAttackBoxes[curAttack] != attackBox) return;
        attackHitReported = true;
        OnAttackHit(curAttack);
    }
    public void AttackEnd()//有效攻击结束
    {
        if (curAttack!=-1&&AttackBoxes[curAttack].gameObject.activeSelf)
            AttackBoxes[curAttack].gameObject.SetActive(false);
    }
    public void ChangeCheckBox(bool r)//改变检查箱方向
    {
        for (int i = 0; i < attackNum; i++)
            ChangeBoxFace(CheckAttackBoxes[i], r);
    }
    private void ChangeBoxFace(BoxCollider2D box,bool r)
    {
        Vector2 offset = box.offset;
        offset.x = Mathf.Abs(offset.x) * (r ? 1f : -1f);
        box.offset= offset;
    }
    public void ChangeAttackBox(bool r) => ChangeBoxFace(AttackBoxes[curAttack], r);
    public void UpdateAttack() => enemyAttackBoxes[curAttack].UpdateAttack();//此时可以再次对玩家造成伤害
    public virtual void UpdateAngry()//攻击、受击增加怒气
    {
        if (!UsesAnger) return;
        curAngry += deltaAngry;
        if (curAngry >= angryFull)
        {
            curAngry = 0;
            AngryRequest = true;
        }
    }
    public void Boost()//愤怒模式
    {
        if (!UsesAnger) return;
        lastAngry = Time.time;
        angry = true;
        for (int i = 0; i < attackNum; i++)
        {
            curAttackDelay[i] *= delayRatio;
        }
    }
    public void BoostOver()//愤怒模式结束
    {
        if (!UsesAnger) return;
        angry = false;
        for (int i = 0; i < attackNum; i++)
        {
            enemyAttackBoxes[i].ReturnBegin(); 
            curAttackDelay[i] = attackDelay[i];
        }
    }
    public void ResetAllAttackCooldowns()//所有招式从此刻重新计算完整冷却，并清掉已准备好的旧请求
    {
        for (int i = 0; i < attackNum; i++)
        {
            lastAttack[i] = Time.time;
            curAttackDelay[i] = attackDelay[i];
        }
        ClearAttackRequests();
    }
}
