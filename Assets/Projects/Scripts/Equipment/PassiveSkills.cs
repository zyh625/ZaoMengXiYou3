using System;
using UnityEngine;

//被动技能：每个角色各自升级七项属性，花金币，永久生效。顺序与 StatUpgradePanel 背景图上的七行一致。
public enum PassiveStat { MaxHP = 0, MaxMP = 1, MaxVigor = 2, Crit = 3, HPRegen = 4, MPRegen = 5, VigorRegen = 6 }

public static class PassiveSkills
{
    public const int Count = 7;
    public static event Action Changed;

    private readonly struct Rule
    {
        public readonly float perLevel;//生命、法力为基础值的比例；暴击为比例；其余为固定数值
        public readonly int maxLevel;
        public Rule(float perLevel, int maxLevel) { this.perLevel = perLevel; this.maxLevel = maxLevel; }
    }

    //按角色定位设置每级加成和等级上限（悟空、八戒、沙僧、唐僧）
    private static readonly Rule[][] Rules =
    {
        new Rule[] { new(0.03f, 10), new(0.02f, 10), new(6f, 10), new(0.015f, 10), new(1f, 2), new(1f, 2), new(2f, 10) },//悟空：暴击、气力
        new Rule[] { new(0.06f, 10), new(0.02f, 10), new(4f, 10), new(0.005f, 10), new(1f, 5), new(1f, 2), new(1f, 10) },//八戒：血量、回血
        new Rule[] { new(0.04f, 10), new(0.04f, 10), new(5f, 10), new(0.01f, 10), new(1f, 3), new(1f, 3), new(1f, 10) },//沙僧：均衡
        new Rule[] { new(0.04f, 10), new(0.05f, 10), new(4f, 10), new(0.01f, 10), new(1f, 3), new(1f, 4), new(1f, 10) },//唐僧：法力、回蓝
    };

    //升到下一级的花费 = 起步价 × 1.3^当前等级，取整到10
    private static readonly int[] BaseCost = { 60, 50, 40, 80, 200, 200, 40 };
    private const float CostGrowth = 1.3f;

    public static int MaxLevel(PlayerId id, PassiveStat stat) => Rules[(int)id][(int)stat].maxLevel;

    public static int Level(PlayerData player, PassiveStat stat)
    {
        if (player == null) return 0;
        player.Normalize();
        return Mathf.Clamp(player.passiveLevels[(int)stat], 0, MaxLevel(player.id, stat));
    }

    //该等级提供的加成：生命、法力返回固定数值（按图鉴基础值折算），暴击返回比例，其余返回固定数值
    public static float Bonus(PlayerId id, PassiveStat stat, int level)
    {
        Rule rule = Rules[(int)id][(int)stat];
        float amount = rule.perLevel * Mathf.Clamp(level, 0, rule.maxLevel);
        return stat switch
        {
            PassiveStat.MaxHP => amount * EquipmentRules.HP(id),
            PassiveStat.MaxMP => amount * EquipmentRules.MP(id),
            _ => amount
        };
    }

    public static float BonusRatio(PlayerId id, PassiveStat stat, int level)//生命、法力按基础值的比例，供属性计算使用
        => Rules[(int)id][(int)stat].perLevel * Mathf.Clamp(level, 0, Rules[(int)id][(int)stat].maxLevel);

    public static int Cost(PlayerId id, PassiveStat stat, int level)//从 level 升到 level+1；已满级返回0
    {
        if (level >= MaxLevel(id, stat)) return 0;
        return Mathf.RoundToInt(BaseCost[(int)stat] * Mathf.Pow(CostGrowth, level) / 10f) * 10;
    }

    public static bool TryUpgrade(SaveData data, PlayerId id, PassiveStat stat)//扣费与升级要么都成功，要么都不变
    {
        PlayerData player = data?.GetPlayer(id);
        if (player == null) return false;
        int level = Level(player, stat);
        int cost = Cost(id, stat, level);
        if (cost <= 0 || !data.TrySpendGold(cost)) return false;
        player.passiveLevels[(int)stat] = level + 1;
        Changed?.Invoke();
        GameSession.instance?.NotifyDataChanged();
        return true;
    }
}
