using System;
using UnityEngine;

public class PlayerResources : MonoBehaviour
{
    [SerializeField] private PlayerId playerId;//与现有存档中的角色编号一致
    [SerializeField, Min(0f)] private float maxMana = 100f;//角色自己的法力上限，进入关卡时回满
    [SerializeField, Min(1f)] private float experienceToNextLevel = 100f;//当前经验槽的显示上限，升级规则以后再接入
    [SerializeField] private float currentMana;//换人只停用对象，不重置已经消耗的法力
    [SerializeField] private float experience;//与现有PlayerData.exp共用经验数据，不另建存档字段
    [SerializeField, Min(0f)] private float maxVigor = 100f;//气力上限，进入关卡和换人时回满
    [SerializeField] private float currentVigor;//气力只显示能量槽，不额外维护数字文本
    [SerializeField, Min(0f)] private float runVigorPerSecond = 5f;//地面跑步和空中快速水平移动每秒消耗
    [SerializeField, Min(0f)] private float jumpVigorCost = 10f;//每次成功起跳的瞬间消耗
    [SerializeField, Min(0f)] private float idleVigorRecovery = 10f;//静止时每秒恢复的气力
    [SerializeField, Min(0f)] private float attackedVigorRecovery = 10f;//受击状态每秒恢复的气力
    private PlayerData playerData;//当前角色在已选存档中的数据；直接运行关卡时允许没有存档
    private bool initialized;//组件的Awake先后顺序不影响资源读取或消费
    private PlayerBoost playerBoost;//无双期间统一锁住气力，所有扣费和恢复入口共用此判断
    public PlayerId Id => playerId;//无双等角色差异直接使用已有存档角色编号

    public float CurrentMana { get { Initialize(); return currentMana; } }
    public float MaxMana { get { Initialize(); return maxMana; } }
    public float Experience { get { Initialize(); return experience; } }
    public int Level { get { Initialize(); return playerData != null ? playerData.playerLevel : 1; } }//等级与经验均直接对应当前角色存档
    public float ExperienceToNextLevel { get { Initialize(); return experienceToNextLevel; } }
    public float CurrentVigor { get { Initialize(); return currentVigor; } }
    public float MaxVigor { get { Initialize(); return maxVigor; } }
    public bool VigorLocked { get { Initialize(); return playerBoost != null && playerBoost.Active; } }//零气力开启无双后也能正常跑跳和释放组合技
    public float RunVigorPerSecond => Mathf.Max(0f, runVigorPerSecond);//移动系统读取配置，资源组件不自行判断动作
    public float JumpVigorCost => Mathf.Max(0f, jumpVigorCost);
    public float IdleVigorRecovery => Mathf.Max(0f, idleVigorRecovery);
    public float AttackedVigorRecovery => Mathf.Max(0f, attackedVigorRecovery);
    public event Action Changed;//法力、经验或气力变化时只通知当前绑定的UI

    private void Awake() => Initialize();

    private void Initialize()//每次创建人物只初始化一次，禁用与重新启用不会回满法力或丢失经验
    {
        if (initialized) return;
        initialized = true;
        playerBoost = GetComponent<PlayerBoost>();
        maxMana = Mathf.Max(0f, maxMana);
        experienceToNextLevel = Mathf.Max(1f, experienceToNextLevel);
        currentMana = maxMana;
        maxVigor = Mathf.Max(0f, maxVigor);
        currentVigor = maxVigor;
        playerData = GameSession.EnsureSession().EnsureRuntimeData().GetPlayer(playerId);
        experience = Mathf.Max(0f, playerData != null ? playerData.exp : experience);
    }

    public bool TrySpendMana(float amount)//法力不足不扣费；技能在确认开始时才调用一次
    {
        Initialize();
        amount = Mathf.Max(0f, amount);
        if (currentMana < amount) return false;
        if (amount == 0f) return true;
        currentMana -= amount;
        Changed?.Invoke();
        return true;
    }

    public void RecoverMana(float amount)//接住蓝色音符等恢复效果共用入口，实时通知现有蓝条
    {
        Initialize();
        if (amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount)) return;
        float recovered = Mathf.Min(maxMana, currentMana + amount);
        if (recovered == currentMana) return;
        currentMana = recovered;
        Changed?.Invoke();
    }

    public void AddExperience(float amount)//经验立即回写现有存档对象，由原有保存流程统一写盘
    {
        Initialize();
        if (amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount)) return;
        SetExperience(Mathf.Min(float.MaxValue, experience + amount));
    }

    public void SetExperience(float amount)//这里只同步数据，不擅自设计经验升级曲线；以后升级系统可以统一调用
    {
        Initialize();
        if (float.IsNaN(amount) || float.IsInfinity(amount)) return;
        amount = Mathf.Max(0f, amount);
        if (experience == amount) return;
        experience = amount;
        if (playerData != null) playerData.exp = experience;
        Changed?.Invoke();
    }

    public void SetLevel(int level)//奖励或升级系统修改等级时立即同步到同一份角色存档
    {
        Initialize();
        if (playerData == null) return;
        level = Mathf.Max(1, level);
        if (playerData.playerLevel == level) return;
        playerData.playerLevel = level;
        Changed?.Invoke();
    }

    public int GetSkillLevel(int skillNumber)//保持与Combat编号一致，从1开始
    {
        Initialize();
        return playerData != null ? playerData.GetSkillLevel(skillNumber) : 0;
    }

    public void SetSkillLevel(int skillNumber, int level)//直接奖励技能时可调用；商店购买走GameSession的扣金币入口
    {
        Initialize();
        if (playerData == null || skillNumber < 1) return;
        playerData.SetSkillLevel(skillNumber, level);
        Changed?.Invoke();
    }

    public bool TrySpendVigor(float amount)//一次性动作只有气力足够才扣费，由调用方确认动作可以成功后调用
    {
        Initialize();
        if (VigorLocked) return true;
        amount = Mathf.Max(0f, amount);
        if (currentVigor < amount) return false;
        SetVigor(currentVigor - amount);
        return true;
    }

    public bool DrainVigor(float amount)//持续动作不足以支付本次消耗时归零并返回false，便于立即结束动作
    {
        Initialize();
        if (VigorLocked) return true;
        amount = Mathf.Max(0f, amount);
        bool sufficient = currentVigor >= amount;
        SetVigor(currentVigor - amount);
        return sufficient;
    }

    public void RecoverVigor(float amount)//具体哪些状态恢复由控制器决定，不在这里自行计时
    {
        Initialize();
        if (VigorLocked) return;
        SetVigor(currentVigor + Mathf.Max(0f, amount));
    }

    public void ResetVigor()//真实换人时显式回满，普通读取和启用不会重复重置
    {
        Initialize();
        SetVigor(maxVigor);
    }

    private void SetVigor(float value)//统一夹紧并且只在数值变化时通知能量槽
    {
        float previous = currentVigor;
        currentVigor = Mathf.Clamp(value, 0f, maxVigor);
        if (currentVigor != previous) Changed?.Invoke();
    }
}
