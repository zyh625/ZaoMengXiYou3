using UnityEngine;

//角色最终属性：图鉴基础值 × 等级成长 × 装备。由 PlayerResources 自动挂上，不需要改预制体。
//换人、重生和驱散会清空各来源的加成，所以每次启用和驱散后都重新套用一遍。
public sealed class PlayerStats : MonoBehaviour
{
    private Health health;
    private PlayerResources resources;
    private PlayerCombat combat;
    private PlayerId id;
    private int appliedLevel = -1;
    private float hpRegen, mpRegen;//每秒恢复量

    public float MaxHP { get; private set; }
    public float MaxMP { get; private set; }
    public float AttackScale { get; private set; } = 1f;//基础攻击 × 等级 × 武器，不含无双等临时加成
    public float CritBonus { get; private set; }//装备提供的暴击率
    public float Reduction { get; private set; }//基础+装备的减伤率
    public float HPRegenPer5s => hpRegen * EquipmentRules.RegenPeriod;
    public float MPRegenPer5s => mpRegen * EquipmentRules.RegenPeriod;
    public int WeaponRarity { get; private set; } = -1;
    public int ArmorRarity { get; private set; } = -1;
    public event System.Action Refreshed;

    private void Awake()
    {
        health = GetComponent<Health>();
        resources = GetComponent<PlayerResources>();
        combat = GetComponent<PlayerCombat>();
        id = resources != null ? resources.Id : PlayerId.WuKong;
        if (GetComponent<EquipmentAura>() == null) gameObject.AddComponent<EquipmentAura>();
        if (GetComponent<TreasureUser>() == null) gameObject.AddComponent<TreasureUser>();//法宝：脑后跟随、按 H 使用
        if (GetComponent<PlayerFx>() == null) gameObject.AddComponent<PlayerFx>();//升级、上场特效
    }

    private void OnEnable()
    {
        EquipmentService.Changed += Apply;
        PassiveSkills.Changed += Apply;
        if (resources != null) resources.Changed += OnResourcesChanged;
        if (health != null) health.DebuffsCleared += Apply;
        Apply();
    }

    private void Start() => Apply();//首次启用时其他组件的OnEnable可能晚于本组件，在全部启用后再套用一次

    private void OnDisable()
    {
        EquipmentService.Changed -= Apply;
        PassiveSkills.Changed -= Apply;
        if (resources != null) resources.Changed -= OnResourcesChanged;
        if (health != null) health.DebuffsCleared -= Apply;
    }

    private void OnResourcesChanged()//法力、经验每帧都可能变化，只有等级变了才重算
    {
        if (resources != null && resources.Level != appliedLevel) Apply();
    }

    public void Apply()
    {
        //停用过程中（换人、退出场景）Health 也会发出驱散通知；此时不重算，避免退出时重新创建已销毁的 GameSession
        if (!isActiveAndEnabled || GameSession.instance == null && !Application.isPlaying) return;
        int level = resources != null ? resources.Level : 1;
        appliedLevel = level;
        SaveData data = GameSession.EnsureSession().EnsureRuntimeData();
        float weaponAttack = 0f, crit = 0f, armorHP = 0f, armorReduction = 0f;
        hpRegen = mpRegen = 0f;
        WeaponRarity = ArmorRarity = -1;

        Read(data, EquipSlot.Weapon, (g, r) =>
        {
            float p = EquipmentRules.Power(g, r);
            weaponAttack = EquipmentRules.WeaponAttack * p;
            crit += EquipmentRules.WeaponCrit * EquipmentRules.WeaponCritScale(id) * p;
            WeaponRarity = r;
        });
        Read(data, EquipSlot.Armor, (g, r) =>
        {
            float p = EquipmentRules.Power(g, r);
            armorHP = EquipmentRules.ArmorHP * EquipmentRules.ArmorHPScale(id) * p;
            armorReduction = EquipmentRules.ArmorReduction * EquipmentRules.ArmorReductionScale(id) * p;
            ArmorRarity = r;
        });
        Read(data, EquipSlot.Accessory, (g, r) =>
        {
            float p = EquipmentRules.Power(g, r);
            crit += EquipmentRules.AccessoryCrit * p;
            hpRegen = EquipmentRules.AccessoryHPRegen * p / EquipmentRules.RegenPeriod;
            mpRegen = EquipmentRules.AccessoryMPRegen * p / EquipmentRules.RegenPeriod;
        });

        //法宝的常驻灵蕴：按等级给一项小属性；主动功能以后单独实现
        float treasureAttack = 0f, treasureReduction = 0f;
        EquipmentItem treasureItem = EquipmentService.Equipped(data, id, EquipSlot.Treasure);
        EquipmentDatabase.Item treasureDef = treasureItem != null ? EquipmentDatabase.Instance?.Find(treasureItem.id) : null;
        if (TreasureRules.Is(treasureDef))
        {
            TreasureKind kind = TreasureRules.Kind(treasureDef);
            int tl = TreasureRules.Level(treasureItem);
            treasureAttack = TreasureRules.AttackBonus(kind, tl);
            treasureReduction = TreasureRules.ReductionBonus(kind, tl);
            crit += TreasureRules.CritBonus(kind, tl);
            hpRegen += TreasureRules.HPRegenPer5s(kind, tl) / EquipmentRules.RegenPeriod;
            mpRegen += TreasureRules.MPRegenPer5s(kind, tl) / EquipmentRules.RegenPeriod;
        }

        //被动技能：生命、法力按基础值比例加到等级成长上，再乘装备；暴击、回复直接相加
        PlayerData passive = data.GetPlayer(id);
        int Lv(PassiveStat stat) => PassiveSkills.Level(passive, stat);
        crit += PassiveSkills.BonusRatio(id, PassiveStat.Crit, Lv(PassiveStat.Crit));
        hpRegen += PassiveSkills.Bonus(id, PassiveStat.HPRegen, Lv(PassiveStat.HPRegen));
        mpRegen += PassiveSkills.Bonus(id, PassiveStat.MPRegen, Lv(PassiveStat.MPRegen));

        MaxHP = EquipmentRules.HP(id) * (EquipmentRules.LevelScale(level, EquipmentRules.HPPerLevel)
            + PassiveSkills.BonusRatio(id, PassiveStat.MaxHP, Lv(PassiveStat.MaxHP))) * (1f + armorHP);
        MaxMP = EquipmentRules.MP(id) * (EquipmentRules.LevelScale(level, EquipmentRules.MPPerLevel)
            + PassiveSkills.BonusRatio(id, PassiveStat.MaxMP, Lv(PassiveStat.MaxMP)));
        AttackScale = EquipmentRules.Attack(id) * EquipmentRules.LevelScale(level, EquipmentRules.AttackPerLevel) * (1f + weaponAttack + treasureAttack);
        CritBonus = crit;
        Reduction = Mathf.Min(EquipmentRules.ReductionCap, EquipmentRules.Reduction(id) + armorReduction + treasureReduction);

        if (health != null)
        {
            health.SetMaxBloodKeepRatio(MaxHP);
            if (Reduction > 0f) health.SetDamageMultiplier(this, 1f - Reduction); else health.RemoveDamageMultiplier(this);
        }
        if (resources != null)
        {
            resources.SetMaxMana(MaxMP);
            resources.SetVigorBonus(PassiveSkills.Bonus(id, PassiveStat.MaxVigor, Lv(PassiveStat.MaxVigor)),
                PassiveSkills.Bonus(id, PassiveStat.VigorRegen, Lv(PassiveStat.VigorRegen)));
        }
        if (combat != null)
        {
            combat.SetAttackMultiplier(this, AttackScale);
            combat.SetCriticalChanceBonus(this, CritBonus);
        }
        Refreshed?.Invoke();
    }

    private void Read(SaveData data, EquipSlot slot, System.Action<float, int> use)
    {
        EquipmentItem item = EquipmentService.Equipped(data, id, slot);
        EquipmentDatabase.Item def = item != null ? EquipmentDatabase.Instance?.Find(item.id) : null;
        if (def != null) use(def.grade, item.rarity);
    }

    private void Update()
    {
        if (health == null || health.Dead || !BattleRun.IsRunning && !TrainingSession.Active) return;
        float dt = Time.deltaTime;//暂停时为0
        if (hpRegen > 0f && health.CurBlood < health.BeginBlood) health.Heal(hpRegen * dt);
        if (mpRegen > 0f && resources != null && resources.CurrentMana < resources.MaxMana) resources.RecoverMana(mpRegen * dt);
    }
}
