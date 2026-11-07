using System;
using UnityEngine;

public enum EquipSlot { Weapon = 0, Armor = 1, Accessory = 2, Treasure = 3 }
public enum EquipRarity { Common = 0, Fine = 1, Superior = 2, Epic = 3, Legendary = 4 }

[Serializable]
public class EquipmentItem//存档里的一件装备实例；同一种装备可以有多件、品质可以不同
{
    public int uid;
    public string id;
    public int rarity;
}

//装备、等级与角色基础属性的全部数值规则。数值推导见 Docs/装备与数值方案.md。
public static class EquipmentRules
{
    //图鉴定下的基础属性：生命、法力、攻击倍率、暴击率、减伤率（按 PlayerId 顺序：悟空、八戒、沙僧、唐僧）
    private static readonly float[] BaseHP = { 95f, 170f, 115f, 80f };
    private static readonly float[] BaseMP = { 90f, 90f, 115f, 135f };
    private static readonly float[] BaseAttack = { 1.00f, 0.95f, 1.00f, 1.18f };
    private static readonly float[] BaseCrit = { 0.12f, 0.05f, 0.06f, 0.08f };
    private static readonly float[] BaseReduction = { 0f, 0.12f, 0.03f, 0f };

    public const int MaxLevel = 60;
    public const float HPPerLevel = 0.02f, AttackPerLevel = 0.012f, MPPerLevel = 0.015f;//每级按基础值的比例成长（生命让出一部分给被动技能）
    public const float CritCap = 0.75f, ReductionCap = 0.60f;//含无双等临时加成的总暴击上限；装备+基础减伤上限
    public const float RegenPeriod = 5f;//回血、回蓝显示为每5秒恢复量
    public const float SwitchCooldownCut = 0.4f;//换人冷却随等级缩短，满级时为基础值的60%

    public static float HP(PlayerId id) => BaseHP[(int)id];
    public static float MP(PlayerId id) => BaseMP[(int)id];
    public static float Attack(PlayerId id) => BaseAttack[(int)id];
    public static float Crit(PlayerId id) => BaseCrit[(int)id];
    public static float Reduction(PlayerId id) => BaseReduction[(int)id];

    public static float ExperienceToNext(int level) => 80f + 25f * Mathf.Clamp(level, 1, MaxLevel);//1级升2级105，59级升60级1555
    public static float LevelScale(int level, float perLevel) => 1f + perLevel * (Mathf.Clamp(level, 1, MaxLevel) - 1);
    public static float SwitchCooldownScale(int level) => 1f - SwitchCooldownCut * (Mathf.Clamp(level, 1, MaxLevel) - 1) / (MaxLevel - 1f);

    //装备属性 = 部位基础值 × 档次系数 × 品质系数。档次系数按该角色同部位的档次位置在 0.4～2.0 之间线性分布。
    private static readonly float[] RarityFactor = { 1f, 1.15f, 1.3f, 1.5f, 1.75f };
    public static float GradeFactor(float grade) => 0.4f + 1.6f * Mathf.Clamp01(grade);
    public static float Power(float grade, int rarity) => GradeFactor(grade) * RarityFactor[Mathf.Clamp(rarity, 0, 4)];

    public const float WeaponAttack = 0.33f, WeaponCrit = 0.02f;//传说顶级武器：攻击+115%、暴击+7%（再乘角色系数）
    public const float ArmorHP = 0.10f, ArmorReduction = 0.05f;//传说顶级防具：生命+35%、减伤+17.5%（再乘角色系数）
    public const float AccessoryCrit = 0.06f, AccessoryHPRegen = 2f, AccessoryMPRegen = 3f;//传说顶级饰品：暴击+21%、每5秒回血7、回蓝10.5

    //专属装备按角色定位放大或缩小副属性：悟空武器暴击高，八戒防具减伤和生命高（按 PlayerId 顺序）
    private static readonly float[] WeaponCritWeight = { 1.5f, 0.5f, 1.0f, 1.0f };
    private static readonly float[] ArmorHPWeight = { 0.9f, 1.2f, 1.0f, 0.9f };
    private static readonly float[] ArmorReductionWeight = { 0.8f, 1.5f, 1.1f, 0.8f };
    public static float WeaponCritScale(PlayerId id) => WeaponCritWeight[(int)id];
    public static float ArmorHPScale(PlayerId id) => ArmorHPWeight[(int)id];
    public static float ArmorReductionScale(PlayerId id) => ArmorReductionWeight[(int)id];

    //第N次轮回时首杀保底掉落的品质，以及重复击杀随机掉落允许的最高品质
    private static readonly int[] RarityCapByRebirth = { 1, 1, 2, 2, 3, 3, 3, 4, 4, 4, 4 };
    public static int RarityCap(int rebirth) => RarityCapByRebirth[Mathf.Clamp(rebirth, 0, RarityCapByRebirth.Length - 1)];
    public static int UnlockRebirth(float grade) => Mathf.FloorToInt(Mathf.Clamp01(grade) * 9f + 0.5f);//最高档在第9次轮回开放
    public const float RepeatDropChance = 0.25f;//同一轮回内重复击杀Boss的随机掉落概率
    private static readonly float[] RepeatRarityWeights = { 40f, 30f, 18f, 9f, 3f };
    public static int RollRarity(int rebirth)
    {
        int cap = RarityCap(rebirth);
        float total = 0f;
        for (int i = 0; i <= cap; i++) total += RepeatRarityWeights[i];
        float roll = UnityEngine.Random.value * total;
        for (int i = 0; i <= cap; i++) { roll -= RepeatRarityWeights[i]; if (roll <= 0f) return i; }
        return cap;
    }

    public const int BagCapacity = 25;//背包格数；已装备的不占格子
    public static int SellValue(float grade, int rarity) => Mathf.RoundToInt(60f * Power(grade, rarity));//重复或放不下的装备自动换成金币

    private static readonly string[] RarityNames = { "普通", "优秀", "精良", "史诗", "传说" };
    private static readonly Color[] RarityColors =
    {
        new(0.85f, 0.85f, 0.85f), new(0.40f, 0.92f, 0.40f), new(0.30f, 0.62f, 1f), new(0.78f, 0.42f, 1f), new(1f, 0.66f, 0.16f)
    };
    public static string RarityName(int rarity) => RarityNames[Mathf.Clamp(rarity, 0, 4)];
    public static Color RarityColor(int rarity) => RarityColors[Mathf.Clamp(rarity, 0, 4)];
}
