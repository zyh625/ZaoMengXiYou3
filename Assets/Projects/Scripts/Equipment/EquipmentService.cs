using System;
using System.Collections.Generic;
using UnityEngine;

//装备的获取、穿戴与查询。所有数据都写在 SaveData 上，随原有保存流程写盘。
public static class EquipmentService
{
    public static event Action Changed;//穿戴变化后通知角色属性和界面刷新
    public static event Action<EquipmentItem, EquipmentDatabase.Item, int> Obtained;//获得装备：实例、定义、换成的金币（0表示放进了背包）

    private static EquipmentDatabase DB => EquipmentDatabase.Instance;

    public static void EnsureStarters(SaveData data)//新存档和旧存档都补齐四人的初始武器与防具
    {
        if (data == null || DB == null) return;
        data.equipment ??= new List<EquipmentItem>();
        foreach (EquipmentDatabase.Item item in DB.items)
        {
            if (item == null || !item.starter || item.owner < 0) continue;
            PlayerData player = data.GetPlayer((PlayerId)item.owner);
            if (player == null || GetUid(player, item.slot) != 0) continue;
            EquipmentItem owned = data.equipment.Find(e => e.id == item.id);
            if (owned == null) owned = Add(data, item.id, (int)EquipRarity.Common);
            SetUid(player, item.slot, owned.uid);
        }
    }

    public static EquipmentItem Equipped(SaveData data, PlayerId id, EquipSlot slot)
    {
        if (data == null) return null;
        EnsureStarters(data);
        PlayerData player = data.GetPlayer(id);
        int uid = player != null ? GetUid(player, slot) : 0;
        return uid != 0 ? data.equipment.Find(e => e.uid == uid) : null;
    }

    public static bool IsEquipped(SaveData data, EquipmentItem item)
    {
        if (data == null || item == null) return false;
        foreach (PlayerData player in data.players)
            if (player.weaponUid == item.uid || player.armorUid == item.uid || player.accessoryUid == item.uid || player.treasureUid == item.uid) return true;
        return false;
    }

    public static bool Equip(SaveData data, PlayerId id, EquipmentItem item)//背包界面以后直接调用；饰品会从原来的角色身上取下
    {
        EquipmentDatabase.Item def = item != null ? DB?.Find(item.id) : null;
        if (data == null || def == null || def.owner >= 0 && def.owner != (int)id) return false;
        foreach (PlayerData other in data.players)
            if (GetUid(other, def.slot) == item.uid) SetUid(other, def.slot, 0);
        SetUid(data.GetPlayer(id), def.slot, item.uid);
        Changed?.Invoke();
        return true;
    }

    public static float Score(EquipmentItem item)
    {
        EquipmentDatabase.Item def = item != null ? DB?.Find(item.id) : null;
        return def != null ? EquipmentRules.Power(def.grade, item.rarity) : 0f;
    }

    //Boss死亡时调用：首杀给保底装备，重复击杀按概率随机掉落。返回获得的装备（可能为空）。
    public static EquipmentItem BossDrop(SaveData data, int level, int rebirth, bool firstKill)
    {
        EquipmentDatabase.BossDrop rule = DB?.DropFor(level);
        if (data == null || rule == null) return null;
        rebirth = Mathf.Max(0, rebirth);
        EquipmentDatabase.Item def;
        int rarity;
        if (firstKill)
        {
            def = DB.BestUnlocked(rule.slot, rule.owner, rebirth);
            rarity = EquipmentRules.RarityCap(rebirth);
        }
        else
        {
            if (UnityEngine.Random.value >= EquipmentRules.RepeatDropChance) return null;
            def = DB.RandomUnlocked(rule.slot, rule.owner, rebirth);
            rarity = EquipmentRules.RollRarity(rebirth);
        }
        return def != null ? Grant(data, def, rarity) : null;
    }

    public static EquipmentItem Grant(SaveData data, EquipmentDatabase.Item def, int rarity)
    {
        EnsureStarters(data);
        rarity = Mathf.Clamp(rarity, 0, 4);
        bool generic = def.owner < 0;
        //专属装备已经有同等或更好的同款时直接换成金币；通用饰品可以给其他角色穿，保留
        if (!generic && data.equipment.Exists(e => e.id == def.id && e.rarity >= rarity))
            return Sell(data, def, rarity);
        EquipmentItem item = Add(data, def.id, rarity);
        AutoEquip(data, def, item);
        TrimBag(data);
        bool kept = data.equipment.Contains(item);
        Obtained?.Invoke(item, def, kept ? 0 : EquipmentRules.SellValue(def.grade, rarity));
        GameSession.instance?.NotifyDataChanged();
        return item;
    }

    private static EquipmentItem Sell(SaveData data, EquipmentDatabase.Item def, int rarity)
    {
        int gold = EquipmentRules.SellValue(def.grade, rarity);
        data.AddGold(gold);
        var item = new EquipmentItem { uid = 0, id = def.id, rarity = rarity };
        Obtained?.Invoke(item, def, gold);
        GameSession.instance?.NotifyDataChanged();
        return item;
    }

    private static void AutoEquip(SaveData data, EquipmentDatabase.Item def, EquipmentItem item)//比当前装备更好就自动换上
    {
        if (def.slot == EquipSlot.Treasure) return;//法宝属性以后再定，先只放进背包
        PlayerId target;
        if (def.owner >= 0) target = (PlayerId)def.owner;
        else
        {
            target = PlayerId.WuKong;//饰品给当前饰品最弱的已解锁角色
            float weakest = float.MaxValue;
            foreach (PlayerId id in Enum.GetValues(typeof(PlayerId)))
            {
                if (!data.IsPlayerUnlocked(id)) continue;
                float score = Score(Equipped(data, id, def.slot));
                if (score < weakest) { weakest = score; target = id; }
            }
        }
        if (Score(item) > Score(Equipped(data, target, def.slot))) Equip(data, target, item);
    }

    private static void TrimBag(SaveData data)//未装备的超过背包格数时，把最弱的换成金币
    {
        var loose = new List<EquipmentItem>();
        foreach (EquipmentItem e in data.equipment) if (!IsEquipped(data, e)) loose.Add(e);
        if (loose.Count <= EquipmentRules.BagCapacity) return;
        loose.Sort((a, b) => Score(a).CompareTo(Score(b)));
        for (int i = 0; i < loose.Count - EquipmentRules.BagCapacity; i++)
        {
            EquipmentDatabase.Item def = DB.Find(loose[i].id);
            if (def != null) data.AddGold(EquipmentRules.SellValue(def.grade, loose[i].rarity));
            data.equipment.Remove(loose[i]);
        }
    }

    private static EquipmentItem Add(SaveData data, string id, int rarity)
    {
        var item = new EquipmentItem { uid = Mathf.Max(1, data.nextEquipmentUid), id = id, rarity = rarity };
        data.nextEquipmentUid = item.uid + 1;
        data.equipment.Add(item);
        return item;
    }

    public static int GetUid(PlayerData player, EquipSlot slot) => slot switch
    {
        EquipSlot.Weapon => player.weaponUid, EquipSlot.Armor => player.armorUid,
        EquipSlot.Accessory => player.accessoryUid, _ => player.treasureUid
    };

    private static void SetUid(PlayerData player, EquipSlot slot, int uid)
    {
        if (player == null) return;
        switch (slot)
        {
            case EquipSlot.Weapon: player.weaponUid = uid; break;
            case EquipSlot.Armor: player.armorUid = uid; break;
            case EquipSlot.Accessory: player.accessoryUid = uid; break;
            default: player.treasureUid = uid; break;
        }
    }
}
