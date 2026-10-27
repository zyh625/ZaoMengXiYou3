using System;
using UnityEngine;

[RequireComponent(typeof(Health), typeof(PlayerResources))]
public class PlayerBoost : MonoBehaviour
{
    [SerializeField, Min(1f)] private float boostFull = 100f;//无双能量上限，满后保留到主动释放
    [SerializeField, Min(0f)] private float deltaBoost = 10f;//每次有效命中获得的能量
    [SerializeField, Min(0f)] private float boostReduce = 0.01f;//未满能量时每秒自然衰减量
    [SerializeField, Min(0.01f)] private float boostTime = 10f;//满能量开启后持续的秒数
    [SerializeField, Range(0f, 1f)] private float bajieDamageMultiplier = 0.5f;//八戒无双承受伤害倍率，可与其他减伤叠乘
    [SerializeField, Min(0f)] private float shaMoveMultiplier = 1.25f;//沙僧无双移动速度倍率
    [SerializeField, Min(0f)] private float tangAttackMultiplier = 1.9f;//唐僧无双攻击力倍率
    [SerializeField] private float boostEnergy;//每个人独立积累，换人时清空已经获得的能量
    private bool boost;//本角色当前是否正在无双
    private bool boostRequest;//空格只提交请求，由控制器在死亡之后、受击之前处理
    private bool initialized;//属性读取和Awake顺序不影响组件接线
    private Health health;//死亡或无双开启时的状态清理
    private PlayerResources resources;//沿用角色已有的PlayerId，不重复增加角色编号
    private PlayerMovement movement;//移速增益只通过来源倍率接入
    private PlayerCombat combat;//攻击增益只通过来源倍率接入

    public float Energy { get { Initialize(); return boostEnergy; } }
    public float MaxEnergy => Mathf.Max(1f, boostFull);
    public bool Active => boost;
    public bool Ready => !boost && Energy >= MaxEnergy;
    public event Action Changed;//积能、开启、持续消耗和结束时刷新当前绑定的能量条

    private void Awake() => Initialize();

    private void Initialize()//只缓存组件与限制数值，不在切换角色时清空能量
    {
        if (initialized) return;
        initialized = true;
        health = GetComponent<Health>();
        resources = GetComponent<PlayerResources>();
        movement = GetComponent<PlayerMovement>();
        combat = GetComponent<PlayerCombat>();
        boostEnergy = Mathf.Clamp(boostEnergy, 0f, MaxEnergy);
    }

    private void OnEnable()
    {
        Initialize();
        boostRequest = false;
        health.Died += OnDied;
        Changed?.Invoke();
    }

    public void RequestBoost() => boostRequest = true;//不直接改动画，保证控制器能够统一处理优先级
    public void ClearRequest() => boostRequest = false;//冻结只丢弃尚未响应的空格，不清空能量或提前结束已生效的无双

    public bool ConsumeRequest()//成功开启时返回true，控制器据此退出当前受击动画
    {
        Initialize();
        bool requested = boostRequest;
        boostRequest = false;
        if (!requested || !isActiveAndEnabled || health.Dead || boost || !Ready) return false;
        health.ClearDebuffs();
        health.AttackedEnd();
        boost = true;
        ApplyBoost();
        Changed?.Invoke();
        return true;
    }

    public void AddEnergy()//无双期间不能一边释放一边靠多段攻击续能
    {
        Initialize();
        if (!isActiveAndEnabled || health.Dead || boost) return;
        float previous = boostEnergy;//满能量或零增量不发送重复通知
        boostEnergy = Mathf.Min(MaxEnergy, boostEnergy + Mathf.Max(0f, deltaBoost));
        if (boostEnergy != previous) Changed?.Invoke();
    }

    private void Update()
    {
        if (health.Dead)
        {
            boostRequest = false;
            EndBoost();
            return;
        }
        float previous = boostEnergy;//以秒计时，不依赖运行帧数
        if (boost)
        {
            boostEnergy = Mathf.Max(0f, boostEnergy - MaxEnergy / Mathf.Max(0.01f, boostTime) * Time.deltaTime);
            if (boostEnergy <= 0f)
            {
                EndBoost();
                return;
            }
        }
        else if (boostEnergy < MaxEnergy)
            boostEnergy = Mathf.Max(0f, boostEnergy - Mathf.Max(0f, boostReduce) * Time.deltaTime);
        if (boostEnergy != previous) Changed?.Invoke();
    }

    private void ApplyBoost()//四角色的持续效果独立，不给所有角色统一套霸体
    {
        switch (resources.Id)
        {
            case PlayerId.WuKong:
                health.SetSuperArmor(this, true);
                break;
            case PlayerId.BaJie:
                health.SetDamageMultiplier(this, bajieDamageMultiplier);
                break;
            case PlayerId.Sha:
                if (movement != null) movement.SetMoveMultiplier(this, shaMoveMultiplier);
                break;
            case PlayerId.Tang:
                if (combat != null) combat.SetAttackMultiplier(this, tangAttackMultiplier);
                break;
        }
    }

    private void EndBoost()//只撤销本组件的来源，圣盾、反甲和领域的增益仍然保留
    {
        if (!boost) return;
        boost = false;
        health.SetSuperArmor(this, false);
        health.RemoveDamageMultiplier(this);
        if (movement != null) movement.RemoveMoveMultiplier(this);
        if (combat != null) combat.RemoveAttackMultiplier(this);
        Changed?.Invoke();
    }

    public void ResetBoost()//换人清空无双、待处理请求及本来源增益，不留下满槽或激活状态
    {
        Initialize();
        bool changed = boost || boostRequest || boostEnergy != 0f;//已清空后再次禁用不发送重复通知
        boostRequest = false;
        boostEnergy = 0f;
        if (boost) EndBoost();
        else if (changed) Changed?.Invoke();
    }

    private void OnDied(DamageInfo damage)//死亡事件立即撤效果，不等待Update或死亡动画
    {
        ResetBoost();
    }

    private void OnDisable()
    {
        if (!initialized) return;
        health.Died -= OnDied;
        ResetBoost();//换人和外部停用均结束效果并清空无双能量
    }
}
