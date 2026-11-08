using System.Collections.Generic;
using UnityEngine;

//法宝：没有品质，只有等级（1～10），花金币在法宝界面升级。每件法宝 = 一项常驻“灵蕴”属性 + 一个主动功能。
//主动功能的战斗逻辑以后再写，这里先定数值，界面和说明都从这里取。数值推导见 Docs/装备与数值方案.md 第 11 节。
public enum TreasureKind { Leaf = 0, Gourd = 1, Sword = 2, Umbrella = 3, Bell = 4 }

public static class TreasureRules
{
    public const int MaxLevel = 10;
    public static readonly Color Color = new(0.42f, 0.90f, 0.80f);//法宝统一用青玉色，不按品质着色

    //从 N 级升到 N+1 级的金币（下标 0 为 1→2）。每件满级共 9600，五件共 4.8 万，与主动、被动技能同一量级。
    private static readonly int[] UpgradeCost = { 200, 300, 450, 650, 900, 1200, 1550, 1950, 2400 };

    public static bool Is(EquipmentDatabase.Item def) => def != null && def.slot == EquipSlot.Treasure;
    public static TreasureKind Kind(EquipmentDatabase.Item def) => (TreasureKind)Mathf.Clamp(def.tier - 1, 0, 4);//数据库里的 tier 1～5 对应素材 1～5.png
    public static int Level(EquipmentItem item) => item == null ? 1 : Mathf.Clamp(item.level, 1, MaxLevel);//旧存档没有等级字段，按 1 级
    public static int Cost(int level) => level >= MaxLevel ? 0 : UpgradeCost[Mathf.Clamp(level, 1, MaxLevel - 1) - 1];

    public static int Spent(int level)
    {
        int sum = 0;
        for (int i = 1; i < Mathf.Clamp(level, 1, MaxLevel); i++) sum += Cost(i);
        return sum;
    }

    public static int SellValue(int level) => 30 + Mathf.RoundToInt(0.1f * Spent(level));//和装备一样压低：1级30，满级约990

    public static bool TryUpgrade(SaveData data, EquipmentItem item)//扣费与升级要么都成功，要么都不变
    {
        EquipmentDatabase.Item def = item != null ? EquipmentDatabase.Instance?.Find(item.id) : null;
        if (data == null || !Is(def)) return false;
        int level = Level(item);
        int cost = Cost(level);
        if (cost <= 0 || !data.TrySpendGold(cost)) return false;
        item.level = level + 1;
        EquipmentService.NotifyChanged();
        GameSession.instance?.NotifyDataChanged();
        return true;
    }

    //---------- 数值：都是 1 级到 10 级线性变化 ----------
    private static float Lerp(float lv1, float lv10, int level) => lv1 + (lv10 - lv1) * (Mathf.Clamp(level, 1, MaxLevel) - 1) / (MaxLevel - 1f);

    //常驻灵蕴：五件满级时价值大致相当（约等于一件史诗饰品的一半）
    public static float HPRegenPer5s(TreasureKind k, int lv) => k == TreasureKind.Leaf ? Lerp(2f, 6f, lv) : 0f;
    public static float AttackBonus(TreasureKind k, int lv) => k == TreasureKind.Gourd ? Lerp(0.02f, 0.065f, lv) : 0f;
    public static float CritBonus(TreasureKind k, int lv) => k == TreasureKind.Sword ? Lerp(0.015f, 0.06f, lv) : 0f;
    public static float ReductionBonus(TreasureKind k, int lv) => k == TreasureKind.Umbrella ? Lerp(0.015f, 0.06f, lv) : 0f;
    public static float MPRegenPer5s(TreasureKind k, int lv) => k == TreasureKind.Bell ? Lerp(3f, 9f, lv) : 0f;

    public static float Cooldown(TreasureKind k, int lv) => k switch
    {
        TreasureKind.Leaf => Lerp(45f, 36f, lv),
        TreasureKind.Gourd => Lerp(40f, 31f, lv),
        TreasureKind.Sword => Lerp(30f, 21f, lv),
        TreasureKind.Umbrella => Lerp(28f, 19f, lv),
        _ => Lerp(35f, 26f, lv),
    };

    public static float Duration(TreasureKind k, int lv) => k switch
    {
        TreasureKind.Leaf => 8f,
        TreasureKind.Gourd => 6f,
        TreasureKind.Sword => Lerp(4f, 6.7f, lv),
        TreasureKind.Umbrella => Lerp(3f, 4.8f, lv),
        _ => Lerp(1.5f, 2.4f, lv),//铃铛：眩晕时间
    };

    public static float LeafHeal(int lv) => Lerp(0.20f, 0.38f, lv);//持续时间内共恢复最大生命的比例
    public static float GourdDamage(int lv) => Lerp(0.30f, 0.48f, lv);
    public const float GourdSlow = 0.30f;//喝葫芦期间移动速度降低 30%，不随等级变化
    public static float SwordSpeed(int lv) => Lerp(0.50f, 0.77f, lv);//御剑比走路快的比例
    public static float UmbrellaPerfectWindow(int lv) => Lerp(0.30f, 0.48f, lv);//开伞后这段时间内挡下攻击算“完美格挡”
    public const float UmbrellaPerfectCooldown = 2f;//完美格挡后冷却只剩 2 秒（防止每帧连按）
    public static float BellRadius(int lv) => Lerp(3.5f, 5.3f, lv);//世界单位
    public const float BellBossFactor = 0.5f;//Boss 不在霸体时也能被震住，但时间减半

    //---------- 界面文字：每项一行 ----------
    public static List<string> StatLines(TreasureKind k, int lv)
    {
        var lines = new List<string>();
        switch (k)
        {
            case TreasureKind.Leaf: lines.Add($"灵蕴：回血 每5秒 +{HPRegenPer5s(k, lv):0.#}"); break;
            case TreasureKind.Gourd: lines.Add($"灵蕴：攻击力 +{Pct(AttackBonus(k, lv))}"); break;
            case TreasureKind.Sword: lines.Add($"灵蕴：暴击率 +{Pct(CritBonus(k, lv))}"); break;
            case TreasureKind.Umbrella: lines.Add($"灵蕴：减伤 +{Pct(ReductionBonus(k, lv))}"); break;
            default: lines.Add($"灵蕴：回蓝 每5秒 +{MPRegenPer5s(k, lv):0.#}"); break;
        }
        lines.Add($"冷却：{Cooldown(k, lv):0.#} 秒");
        switch (k)
        {
            case TreasureKind.Leaf:
                lines.Add($"回复：{Duration(k, lv):0.#} 秒内共恢复 {Pct(LeafHeal(lv))} 最大生命");
                break;
            case TreasureKind.Gourd:
                lines.Add($"增伤：+{Pct(GourdDamage(lv))}，持续 {Duration(k, lv):0.#} 秒");
                lines.Add($"代价：持续期间移动速度 -{Pct(GourdSlow)}");
                break;
            case TreasureKind.Sword:
                lines.Add($"御剑：持续 {Duration(k, lv):0.#} 秒，移动速度 +{Pct(SwordSpeed(lv))}");
                break;
            case TreasureKind.Umbrella:
                lines.Add($"护体：持续 {Duration(k, lv):0.#} 秒，挡下一次攻击");
                lines.Add($"完美格挡：开伞 {UmbrellaPerfectWindow(lv):0.##} 秒内挡下，冷却立即结束");
                break;
            default:
                lines.Add($"范围：半径 {BellRadius(lv):0.#} 米");
                lines.Add($"眩晕：{Duration(k, lv):0.#} 秒（Boss 减半）");
                break;
        }
        return lines;
    }

    public static string Function(TreasureKind k) => k switch
    {
        TreasureKind.Leaf => "功能（按 H）：使用后缓慢恢复生命，受伤不会打断",
        TreasureKind.Gourd => "功能（按 H）：饮下后伤害大增，但身体沉重、移动变慢",
        TreasureKind.Sword => "功能（按 H）：踏剑飞行，W/K 上升、S 下降；攻击、放技能或被击中时落地",
        TreasureKind.Umbrella => "功能（按 H）：撑开护罩免疫下一次攻击，时机准则冷却立即结束",
        _ => "功能（按 H）：摇铃震慑周围所有非霸体的敌人",
    };

    private static string Pct(float ratio) => $"{ratio * 100f:0.#}%";
}
