using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Combat
{
    public string id;
    public SpriteData animation;
    public BoxCollider2D box;
    public int openFrame, closeFrame;
    [Min(0f), Tooltip("兼容原招式数据；技能实际蓝耗在Resources/Skills/SkillProgression中按等级配置。")]
    public float manaCost = 10f;//保留旧字段和序列化数据，普攻和组合技不消耗蓝量
    [Min(0f), Tooltip("兼容原招式数据；技能实际冷却在Resources/Skills/SkillProgression中按等级配置。")]
    public float cooldown = 10f;//成功释放才开始冷却，实际长度读取当前技能等级配置
    public Sprite skillIcon;//技能栏显示用图标，和角色的动作帧素材分别配置
    public bool advancesThaw = true;//真正开始本招式时推进一帧融化，防御或辅助招式可在配置中关闭
    public bool continuous;//组合技是否持续释放：持续型按秒消耗，一次性只在进入时扣除
    [Min(0f)] public float vigorCost = 20f;//一次性组合技成功释放时的气力消耗
    [Min(0f)] public float vigorPerSecond = 10f;//持续型组合技每秒消耗的气力

    public bool CanOpen(int idx) => idx >= openFrame && idx < closeFrame;
    public bool CanClose(int idx) => idx >= closeFrame;

    public void Open(int idx)
    {
        if (CanOpen(idx) && !box.gameObject.activeSelf)
            box.gameObject.SetActive(true);
    }

    public void Close(int idx)
    {
        if (CanClose(idx) && box.gameObject.activeSelf)
            box.gameObject.SetActive(false);
    }
}

[RequireComponent(typeof(PlayerResources))]
public class PlayerCombat : MonoBehaviour
{
    [SerializeField] private Combat[] allAttacks;
    private int normalNum, comboNum, skillNum;
    private readonly Dictionary<string, Combat> actions = new();
    private Combat[] normalAttacks, comboAttacks, skills;//按编号缓存招式，0号留空，与Attacked、Comboed、Skilled的编号一致
    private Combat riseAttack;//空中普攻的数据，独立于地面普攻连招
    private float[] skillCooldownUntil;//按角色、技能编号保存绝对到期时间，换人停用期间仍自然计时
    private float[] skillCooldownDurations;//保存该次释放的实际冷却长度，运行中修改配置不改变旧冷却比例
    public int SkillCount => skillNum;//界面可展示前几个槽，战斗数据仍允许以后继续增加技能
    public event System.Action<int> SkillCooldownStarted;//仅实际扣蓝并成功接受后通知界面立即盖满冷却遮罩

    private bool attackRequest = false;//请求攻击
    private float attackPri = -1f;//最多保存一次攻击
    private float attackCur = -1f;//最近一次按下攻击的时间
    private bool pri = false;//这次普通攻击响应的是上次按下的还是最近按下的
    [SerializeField] private float attackValid = 0.8f;//攻击请求有效期
    public int Attacked = -1;
    public int Comboed = -1;
    private int curAttack = 0;//下次攻击的招式的前一招
    private float lastAttack = -1f;
    public bool AttackThisFrame = false;
    [SerializeField] private float attackDelay = 0.7f;//攻击间隔
    [SerializeField] private float attackMemory = 1.2f;//攻击连招保存
    public int Skilled = -1;
    private bool attackedRequest = false;
    public bool AttackedRequest => attackedRequest;
    private PlayerResources resources;//每个角色自己的蓝量和经验，换人不会重置
    private PlayerBoost playerBoost;//命中积累无双，主动攻击与反伤分别结算
    private Health health;//攻击力降低等负面效果参与统一驱散
    private readonly Dictionary<Object, float> attackMultipliers = new();//攻击力倍率按来源叠加，不修改攻击箱的基础伤害
    [SerializeField, Range(0f, 1f)] private float criticalChance = 0f;//角色基础暴击率，0.3表示30%，不包含临时增益
    [SerializeField, Min(1f)] private float criticalMultiplier = 2f;//发生暴击时的伤害倍率
    private readonly Dictionary<Object, float> criticalChanceBonuses = new();//暴击率按来源相加，结束无双只移除自己的加成

    protected virtual void Awake()
    {
        resources = GetComponent<PlayerResources>();
        playerBoost = GetComponent<PlayerBoost>();
        health = GetComponent<Health>();
        if (health != null) health.DebuffsCleared += ClearAttackDebuffs;
        foreach (Combat action in allAttacks)
        {
            if (action == null || string.IsNullOrWhiteSpace(action.id))
            {
                Debug.LogError("存在未填写名称的招式", this);
                continue;
            }
            if (!actions.TryAdd(action.id, action))
                Debug.LogError($"招式{action.id}重名", this);
        }

        normalAttacks = GetActions("NormalAttack", out normalNum);
        comboAttacks = GetActions("ComboAttack", out comboNum);
        skills = GetActions("Skill", out skillNum);
        skillCooldownUntil = new float[skills.Length];
        skillCooldownDurations = new float[skills.Length];
        riseAttack = GetAction("RiseAttack1");
    }

    public Combat GetAction(string id) =>
        actions.TryGetValue(id, out var action) ? action : null;

    //只在初始化时按连续编号整理数据，战斗过程中直接通过数组访问。
    private Combat[] GetActions(string prefix, out int count)
    {
        List<Combat> result = new() { null };//0号留空，让招式编号从1开始
        count = 0;
        while (actions.TryGetValue($"{prefix}{count + 1}", out var action))
        {
            result.Add(action);
            count++;
        }
        return result.ToArray();
    }

    public virtual void OnAttack()
    {
        if (health != null && health.Status != null && health.Status.BlocksActions) return;
        attackRequest = true;
        attackPri = attackCur;
        attackCur = Time.time;
    }

    public void CheckAttack()
    {
        AttackThisFrame = false;
        if (lastAttack != -1f && Time.time - lastAttack <= attackDelay) return;//攻击间隔过短
        if (attackPri != -1f && Time.time - attackPri <= attackValid)
        {
            AttackThisFrame = true;
            pri = true;
        }
        else if (attackRequest)
        {
            attackRequest = false;
            if (Time.time - attackCur <= attackValid)
            {
                AttackThisFrame = true;
                pri = false;
            }
        }
    }

    public void NormalAttack()
    {
        if (lastAttack != -1f && Time.time - lastAttack > attackMemory)
            curAttack = 1;
        else
            curAttack = curAttack == normalNum ? 1 : curAttack + 1;
        Attacked = curAttack;
        lastAttack = Time.time;
        if (pri) attackPri = -1f;
        else attackCur = -1f;
    }

    public virtual void OnSkill(int k)
    {
        if (health != null && health.Status != null && health.Status.BlocksActions) return;
        if (Skilled != -1 || !IsSkillLearned(k) || IsSkillCoolingDown(k)) return;
        Skilled = k;
    }

    public bool TryBeginSkill() => Skilled != -1 && TrySpendSkillMana(Skilled);//只有进入施法动作才扣费，失效请求不扣蓝

    protected bool TrySpendSkillMana(int k)//瞬发技能也通过同一入口扣蓝，无蓝时不播放特效
    {
        if (health != null && health.Status != null && health.Status.BlocksActions) return false;
        if (!IsSkillLearned(k) || IsSkillCoolingDown(k)) return false;
        if (!resources.TrySpendMana(GetSkillManaCost(k))) return false;
        float duration = GetSkillCooldownDuration(k);//按当前已学习等级读取，已开始的旧冷却仍使用当次长度
        skillCooldownDurations[k] = duration;
        skillCooldownUntil[k] = Time.time + duration;
        SkillCooldownStarted?.Invoke(k);
        return true;
    }
    public bool HasSkill(int k)//无本体动画的瞬发技能同样有效，不强制要求攻击箱或动作帧
        => skills != null && k > 0 && k < skills.Length && skills[k] != null;
    public bool IsSkillLearned(int k)//招式存在与已经学习分开，未学技能仍能展示图标但不能接受施法
        => HasSkill(k) && resources != null && resources.GetSkillLevel(k) > 0;
    public float GetSkillManaCost(int k)//界面与战斗读取同一份等级属性，升级后下一次释放立即生效
        => HasSkill(k) && resources != null
            ? SkillProgression.Current.GetLevelData(resources.Id, k, resources.GetSkillLevel(k)).manaCost : 0f;
    public float GetSkillCooldownDuration(int k)//这里只返回新一次施法的冷却，不改变已经开始的到期时间
        => HasSkill(k) && resources != null
            ? SkillProgression.Current.GetLevelData(resources.Id, k, resources.GetSkillLevel(k)).cooldown : 0f;
    public bool IsSkillCoolingDown(int k) => GetSkillCooldownRemaining(k) > 0f;//输入和真正施法时共用同一冷却判断
    public Sprite GetSkillIcon(int k) => HasSkill(k) ? skills[k].skillIcon : null;//界面只读图标，不依赖角色类型
    public float GetSkillCooldownRemaining(int k)//绝对时间无需在Update递减，备用角色也会正常走完冷却
        => HasSkill(k) && skillCooldownUntil != null ? Mathf.Max(0f, skillCooldownUntil[k] - Time.time) : 0f;
    public float GetSkillCooldownRatio(int k)//遮罩显示剩余比例：成功释放为1，冷却结束为0
    {
        if (!HasSkill(k) || skillCooldownDurations == null || skillCooldownDurations[k] <= 0f) return 0f;
        return Mathf.Clamp01(GetSkillCooldownRemaining(k) / skillCooldownDurations[k]);
    }

    public bool ReduceSkillCooldown(int k, float ratio)//按该次释放的总冷却扣减比例，精准抵挡20%不会误算成剩余时间的20%
    {
        if (!HasSkill(k) || !IsSkillCoolingDown(k) || skillCooldownDurations == null || ratio <= 0f) return false;
        skillCooldownUntil[k] = Mathf.Max(Time.time,
            skillCooldownUntil[k] - skillCooldownDurations[k] * Mathf.Clamp01(ratio));
        return true;//界面沿用剩余冷却读取，冷却被提前清空时也能正常触发结束闪烁
    }

    public virtual void SkillEnd() { }

    public void NotifyAttackStarted(string id)//只由控制器在实际切入攻击动作时调用，失败按键和每帧命中不会重复加速融化
    {
        Combat action = GetAction(id);
        if (action != null && action.advancesThaw && health != null)
            health.Status?.NotifyAttackStarted();
    }

    public bool TryBeginCombo(int k)//先校验或支付气力，再由角色控制器真正切入对应组合技
    {
        if (health != null && health.Status != null && health.Status.BlocksActions) return false;
        if (k < 1 || k > comboNum || comboAttacks[k] == null || resources == null) return false;
        if (resources.VigorLocked) return true;//无双冻结当前气力，零气力也能开始本来有效的组合技
        Combat action = comboAttacks[k];//连续型只检查能否维持首个物理步，不重复扣入场费用
        return action.continuous
            ? action.vigorPerSecond <= 0f || resources.CurrentVigor >= action.vigorPerSecond * Time.fixedDeltaTime
            : resources.TrySpendVigor(action.vigorCost);
    }

    public bool DrainComboVigor(float deltaTime)//一次性组合技已在开始时支付；持续型耗尽后由控制器结束
    {
        if (Comboed < 1 || Comboed > comboNum || comboAttacks[Comboed] == null) return false;
        if (resources != null && resources.VigorLocked) return true;//无双期间持续组合技不消耗，也不因零气力退出
        Combat action = comboAttacks[Comboed];
        if (!action.continuous || action.vigorPerSecond <= 0f) return true;
        return resources.DrainVigor(action.vigorPerSecond * deltaTime) && resources.CurrentVigor > 0f;
    }
    public bool ContinuousCombo => Comboed > 0 && Comboed <= comboNum && comboAttacks[Comboed].continuous;//持续动作使用自己的每秒消耗，不再叠算跑步

    public float AttackMultiplier
    {
        get
        {
            float multiplier = 1f;//命中时读取，因此持续攻击期间开启无双也会立即生效
            foreach (KeyValuePair<Object, float> item in attackMultipliers)
                if (item.Key != null) multiplier *= item.Value;
            return multiplier;
        }
    }
    public void SetAttackMultiplier(Object source, float multiplier)
    {
        if (source != null) attackMultipliers[source] = Mathf.Max(0f, multiplier);
    }
    public void RemoveAttackMultiplier(Object source)
    {
        if (source != null) attackMultipliers.Remove(source);
    }

    public float CriticalChance//读取概率本身不投掷随机数，查询界面或其他属性不会额外消耗一次暴击机会
    {
        get
        {
            float chance = criticalChance;
            foreach (KeyValuePair<Object, float> item in criticalChanceBonuses)
                if (item.Key != null) chance += item.Value;
            return Mathf.Clamp01(chance);
        }
    }

    public void SetCriticalChanceBonus(Object source, float bonus)//加成0.3代表额外增加30个百分点，可叠加其他正负来源
    {
        if (source != null) criticalChanceBonuses[source] = bonus;
    }

    public void RemoveCriticalChanceBonus(Object source)
    {
        if (source != null) criticalChanceBonuses.Remove(source);
    }

    public DamageInfo CreateAttackDamage(float baseDamage, bool knockback = true, bool launch = false,
        AttackEffect[] effects = null, Transform source = null, DamageKind kind = DamageKind.Skill)//所有玩家主动伤害只在实际命中入口投掷一次暴击
    {
        float amount = Mathf.Max(0f, baseDamage) * AttackMultiplier;
        float chance = CriticalChance;
        if (amount > 0f && chance > 0f && (chance >= 1f || Random.value < chance))
            amount *= Mathf.Max(1f, criticalMultiplier);
        return new DamageInfo(source != null ? source : transform, amount, knockback, launch, effects, kind: kind);
    }
    private void ClearAttackDebuffs()//只清除攻击降低，正面攻击增益保持
    {
        List<Object> removed = new();//只在清除负面状态时分配
        foreach (KeyValuePair<Object, float> item in attackMultipliers)
            if (item.Key == null || item.Value < 1f) removed.Add(item.Key);
        foreach (Object source in removed) attackMultipliers.Remove(source);
        removed.Clear();
        foreach (KeyValuePair<Object, float> item in criticalChanceBonuses)
            if (item.Key == null || item.Value < 0f) removed.Add(item.Key);
        foreach (Object source in removed) criticalChanceBonuses.Remove(source);
    }
    protected virtual void OnDisable()//换出角色后不保留已经结束的攻击倍率或临时暴击率来源
    {
        attackMultipliers.Clear();
        criticalChanceBonuses.Clear();
    }
    protected virtual void OnDestroy()
    {
        if (health != null) health.DebuffsCleared -= ClearAttackDebuffs;
    }

    public bool ComboOpen(int idx, int k) => comboAttacks[k].CanOpen(idx);
    public bool ComboShut(int idx, int k) => comboAttacks[k].CanClose(idx);
    public bool AttackOpen(int idx, int k) => normalAttacks[k].CanOpen(idx);
    public bool AttackShut(int idx, int k) => normalAttacks[k].CanClose(idx);
    public bool SkillOpen(int idx, int k) => skills[k].CanOpen(idx);
    public virtual bool SkillShut(int idx, int k) => skills[k].CanClose(idx);
    public bool RiseOpen(int idx) => riseAttack.CanOpen(idx);
    public bool RiseShut(int idx) => riseAttack.CanClose(idx);

    public void OpenAttackBox(bool r) => OpenBox(normalAttacks[Attacked].box, r);

    public void ShutAttackBox()
    {
        //普攻结束时Attacked也可能已经为-1，因此关闭所有普攻引用的攻击箱。
        for (int i = 1; i <= normalNum; i++)//i为从1开始的普攻编号
            ShutBox(normalAttacks[i].box);
    }

    public void OpenComboBox(bool r) => OpenBox(comboAttacks[Comboed].box, r);

    public void ShutComboBox()
    {
        if (Comboed != -1)
            ShutBox(comboAttacks[Comboed].box);
    }

    public void OpenSkillBox(bool r) => OpenBox(skills[Skilled].box, r);

    public void ShutSkillBox()
    {
        if (Skilled != -1)
            ShutBox(skills[Skilled].box);
    }

    public void OpenRiseBox(bool r) => OpenBox(riseAttack.box, r);
    public void ShutRiseBox() => ShutBox(riseAttack.box);

    private void OpenBox(BoxCollider2D box, bool r)
    {
        if (box == null) return;//非攻击性技能可以只有动作帧，没有攻击箱
        Vector2 offset = box.offset;
        offset.x = Mathf.Abs(offset.x) * (r ? 1f : -1f);
        box.offset = offset;
        if (!box.gameObject.activeSelf)
            box.gameObject.SetActive(true);
    }

    private void ShutBox(BoxCollider2D box)
    {
        if (box != null && box.gameObject.activeSelf)
            box.gameObject.SetActive(false);
    }

    //换人后不保留上一个角色的攻击缓存、连招进度或技能请求。
    public void ClearRequests()
    {
        ClearAttackRequests();
        lastAttack = -1f;
        curAttack = 0;
        Attacked = Comboed = Skilled = -1;
    }

    public void ClearAttackRequests()//只清除普攻输入，不清掉更高优先级的技能请求
    {
        attackRequest = false;
        attackPri = attackCur = -1f;
        pri = false;
        AttackThisFrame = false;
    }

    public void UpdateBoost()//攻击到敌人，增加无双能量
    {
        if (playerBoost != null) playerBoost.AddEnergy();
    }
}
