using System;
using UnityEngine;

//全部装备定义与Boss掉落部位。资产位于 Resources/Equipment/EquipmentDatabase，可在检视面板直接调整。
[CreateAssetMenu(menuName = "ZaoMeng/Equipment Database")]
public sealed class EquipmentDatabase : ScriptableObject
{
    [Serializable]
    public sealed class Item
    {
        public string id;
        public string displayName;
        [TextArea(2, 4)] public string description;//背包悬停时显示的背景介绍
        public EquipSlot slot;
        [Tooltip("所属角色；-1 表示通用（饰品、法宝）")] public int owner = -1;
        [Tooltip("素材上的档次编号，0 为初始装备")] public int tier;
        [Tooltip("在该角色同部位中的位置，0～1；决定属性和开放的轮回（法宝只用来决定开放轮回）")] [Range(0f, 1f)] public float grade;
        public Sprite icon;
        public bool starter;//新存档自动获得并装备
        public int UnlockRebirth => EquipmentRules.UnlockRebirth(grade);
    }

    [Serializable]
    public sealed class BossDrop
    {
        [Tooltip("关卡编号 1～9")] public int level;
        public EquipSlot slot;
        [Tooltip("掉落给哪个角色；饰品填 -1")] public int owner = -1;
    }

    [Header("Boss 掉落")]
    [Range(0f, 1f), Tooltip("每次击杀Boss掉落装备的概率，所有Boss相同")] public float bossDropChance = 0.4f;
    [Range(0f, 1f), Tooltip("掉落时给该部位当前轮回已开放最高档的概率，其余随机给已开放的档次")] public float bestItemChance = 0.6f;
    [Range(0f, 1f), Tooltip("每次击杀Boss另外掉一件法宝的概率；只掉本轮已开放、还没拥有的法宝")] public float treasureDropChance = 0.12f;
    [Header("小怪掉落")]
    [Range(0f, 1f), Tooltip("小怪死亡时掉一件普通（白色）装备的概率；掉了装备就不再掉金币")] public float minionDropChance = 0.04f;
    public Item[] items = Array.Empty<Item>();
    public BossDrop[] bossDrops = Array.Empty<BossDrop>();

    private static EquipmentDatabase loaded;
    public static EquipmentDatabase Instance => loaded != null ? loaded : loaded = Resources.Load<EquipmentDatabase>("Equipment/EquipmentDatabase");

    public Item Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        foreach (Item item in items) if (item != null && item.id == id) return item;
        return null;
    }

    public BossDrop DropFor(int level)
    {
        foreach (BossDrop drop in bossDrops) if (drop != null && drop.level == level) return drop;
        return null;
    }

    public Item BestUnlocked(EquipSlot slot, int owner, int rebirth)//该部位在当前轮回已开放的最高档
    {
        Item best = null;
        foreach (Item item in items)
            if (item != null && item.slot == slot && item.owner == owner && item.UnlockRebirth <= rebirth && (best == null || item.grade > best.grade))
                best = item;
        return best;
    }

    public Item RandomUnlocked(EquipSlot slot, int owner, int rebirth)
    {
        int count = 0;
        foreach (Item item in items) if (item != null && item.slot == slot && item.owner == owner && item.UnlockRebirth <= rebirth) count++;
        if (count == 0) return null;
        int pick = UnityEngine.Random.Range(0, count);
        foreach (Item item in items)
            if (item != null && item.slot == slot && item.owner == owner && item.UnlockRebirth <= rebirth && pick-- == 0) return item;
        return null;
    }
}
