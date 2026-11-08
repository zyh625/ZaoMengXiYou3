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

    //Boss死亡时调用：按数据库里的概率决定是否掉落，以及掉哪一件、什么品质。只决定掉什么，拾取时才真正入包。
    public static bool RollBossDrop(int level, int rebirth, bool firstKill, out EquipmentDatabase.Item def, out int rarity)
    {
        def = null; rarity = 0;
        EquipmentDatabase.BossDrop rule = DB?.DropFor(level);
        if (rule == null || UnityEngine.Random.value >= DB.bossDropChance) return false;
        rebirth = Mathf.Max(0, rebirth);
        def = UnityEngine.Random.value < DB.bestItemChance ? DB.BestUnlocked(rule.slot, rule.owner, rebirth) : DB.RandomUnlocked(rule.slot, rule.owner, rebirth);
        rarity = firstKill ? EquipmentRules.RarityCap(rebirth) : EquipmentRules.RollRarity(rebirth);//本轮首杀掉落时品质直接到本轮上限
        if (def != null && !EquipmentRules.HasCommon(def.slot)) rarity = Mathf.Max(rarity, (int)EquipRarity.Fine);//饰品没有普通品质
        if (TreasureRules.Is(def)) rarity = 0;//法宝不分品质
        return def != null;
    }

    //Boss死亡时另掷一次法宝：从本轮已开放、且还没拥有的法宝里随机一件。每件法宝只会有一个，靠升级变强。
    public static bool RollTreasureDrop(SaveData data, int rebirth, out EquipmentDatabase.Item def)
    {
        def = null;
        if (DB == null || data == null || UnityEngine.Random.value >= DB.treasureDropChance) return false;
        var pool = new List<EquipmentDatabase.Item>();
        foreach (EquipmentDatabase.Item item in DB.items)
            if (TreasureRules.Is(item) && item.UnlockRebirth <= Mathf.Max(0, rebirth) && !data.equipment.Exists(e => e.id == item.id))
                pool.Add(item);
        if (pool.Count == 0) return false;
        def = pool[UnityEngine.Random.Range(0, pool.Count)];
        return true;
    }

    public static void NotifyChanged() => Changed?.Invoke();//法宝升级后让角色属性和界面刷新

    //【临时调试】一键通关用：先把没有的法宝放进背包，再给四个角色各穿上最高档的传说武器、防具和饰品。
    //换下来的旧装备放回背包；背包满了就直接丢弃，不会出现看不见的多余装备。返回因背包已满没放下的法宝数。
    public static int DebugGrantAll(SaveData data)
    {
        if (data == null || DB == null) return 0;
        data.equipment ??= new List<EquipmentItem>();
        int skipped = 0;
        foreach (EquipmentDatabase.Item def in DB.items)
        {
            if (!TreasureRules.Is(def) || data.equipment.Exists(e => e.id == def.id)) continue;
            if (LooseCount(data) >= EquipmentRules.BagCapacity) { skipped++; continue; }
            Add(data, def.id, 0).level = 1;
        }
        EquipmentDatabase.Item accessory = DB.BestUnlocked(EquipSlot.Accessory, -1, int.MaxValue);
        for (int i = 0; i < 4; i++)
        {
            PlayerData player = data.GetPlayer((PlayerId)i);
            if (player == null) continue;
            DebugEquipTop(data, player, DB.BestUnlocked(EquipSlot.Weapon, i, int.MaxValue));
            DebugEquipTop(data, player, DB.BestUnlocked(EquipSlot.Armor, i, int.MaxValue));
            DebugEquipTop(data, player, accessory);
        }
        Changed?.Invoke();
        GameSession.instance?.NotifyDataChanged();
        return skipped;
    }

    private static void DebugEquipTop(SaveData data, PlayerData player, EquipmentDatabase.Item def)
    {
        if (def == null) return;
        const int top = (int)EquipRarity.Legendary;
        int uid = GetUid(player, def.slot);
        EquipmentItem old = uid != 0 ? data.equipment.Find(e => e.uid == uid) : null;
        if (old != null && old.id == def.id && old.rarity == top) return;//已经穿着同一件传说，重复点击不再发放
        SetUid(player, def.slot, Add(data, def.id, top).uid);
        if (old != null && LooseCount(data) > EquipmentRules.BagCapacity) data.equipment.Remove(old);//旧装备放不下就丢弃
    }

    //小怪：按概率掉一件普通品质装备，从当前轮回已开放、且属于已解锁角色（或通用饰品）的武器、防具、饰品里随机
    public static bool RollMinionDrop(SaveData data, int rebirth, out EquipmentDatabase.Item def)
    {
        def = null;
        if (DB == null || data == null || UnityEngine.Random.value >= DB.minionDropChance) return false;
        var pool = new List<EquipmentDatabase.Item>();
        foreach (EquipmentDatabase.Item item in DB.items)
            if (item != null && EquipmentRules.HasCommon(item.slot) && item.UnlockRebirth <= Mathf.Max(0, rebirth)
                && (item.owner < 0 || data.IsPlayerUnlocked((PlayerId)item.owner)))
                pool.Add(item);
        if (pool.Count == 0) return false;
        def = pool[UnityEngine.Random.Range(0, pool.Count)];
        return true;
    }

    public static int LooseCount(SaveData data)
    {
        int n = 0;
        foreach (EquipmentItem e in data.equipment) if (!IsEquipped(data, e)) n++;
        return n;
    }

    public static List<EquipmentItem> LooseItems(SaveData data)//背包格子里的装备，按获得 / 卸下的先后排列
    {
        EnsureStarters(data);
        var list = new List<EquipmentItem>();
        foreach (EquipmentItem e in data.equipment) if (!IsEquipped(data, e)) list.Add(e);
        return list;
    }

    //拾取：放进背包末尾，不自动穿戴。背包已满时返回 null，掉落物留在地上。
    public static EquipmentItem Grant(SaveData data, EquipmentDatabase.Item def, int rarity)
    {
        EnsureStarters(data);
        if (def == null) return null;
        bool treasure = TreasureRules.Is(def);
        EquipmentItem owned = treasure ? data.equipment.Find(e => e.id == def.id) : null;
        if (owned != null)//法宝每件只有一个：两个Boss先后掉了同一件、或捡起前已经拥有时，重复的换成金币
        {
            int gold = TreasureRules.SellValue(1);
            data.AddGold(gold);
            Obtained?.Invoke(owned, def, gold);
            GameSession.instance?.NotifyDataChanged();
            return owned;
        }
        if (LooseCount(data) >= EquipmentRules.BagCapacity) return null;
        EquipmentItem item = Add(data, def.id, treasure ? 0 : Mathf.Clamp(rarity, 0, 4));
        if (treasure) item.level = 1;
        Obtained?.Invoke(item, def, 0);
        GameSession.instance?.NotifyDataChanged();
        return item;
    }

    //从背包穿到当前角色身上；原来那件被挤回背包末尾
    public static bool EquipFromBag(SaveData data, PlayerId id, EquipmentItem item)
    {
        EquipmentDatabase.Item def = item != null ? DB?.Find(item.id) : null;
        if (data == null || def == null || def.owner >= 0 && def.owner != (int)id) return false;
        if (def.slot == EquipSlot.Treasure && TreasureUser.IsLocked(id)) return false;//法宝冷却中不能更换
        EquipmentItem old = Equipped(data, id, def.slot);
        if (!Equip(data, id, item)) return false;
        if (old != null && old != item) MoveToEnd(data, old);
        GameSession.instance?.NotifyDataChanged();
        return true;
    }

    public static bool Unequip(SaveData data, PlayerId id, EquipSlot slot)//卸下放到背包末尾；背包满了不能卸
    {
        EquipmentItem item = Equipped(data, id, slot);
        if (item == null || LooseCount(data) >= EquipmentRules.BagCapacity) return false;
        if (slot == EquipSlot.Treasure && TreasureUser.IsLocked(id)) return false;//法宝冷却中不能卸下
        SetUid(data.GetPlayer(id), slot, 0);
        MoveToEnd(data, item);
        Changed?.Invoke();
        GameSession.instance?.NotifyDataChanged();
        return true;
    }

    public static int SellItem(SaveData data, EquipmentItem item)//只卖背包里的；返回卖得的金币
    {
        EquipmentDatabase.Item def = item != null ? DB?.Find(item.id) : null;
        if (data == null || def == null || IsEquipped(data, item) || !data.equipment.Remove(item)) return 0;
        int gold = SellPrice(item);
        data.AddGold(gold);
        GameSession.instance?.NotifyDataChanged();
        return gold;
    }

    public static int SellAllCommon(SaveData data, out int count)//“出售白装”：卖掉背包里全部普通品质装备（已穿着的不动）
    {
        count = 0;
        int gold = 0;
        foreach (EquipmentItem item in LooseItems(data))
        {
            if (item.rarity != (int)EquipRarity.Common || TreasureRules.Is(DB?.Find(item.id))) continue;//法宝品质字段为0，但不是白装
            int sold = SellItem(data, item);
            if (sold <= 0) continue;
            gold += sold;
            count++;
        }
        return gold;
    }

    //“整理背包”：法宝不分品质，排在最前（等级从高到低）；其余品质从高到低；同品质按饰品、武器、防具；再按档次从高到低
    private static readonly int[] SlotOrder = { 2, 3, 1, 0 };//按 EquipSlot 下标：武器2、防具3、饰品1、法宝0
    public static void SortBag(SaveData data)
    {
        List<EquipmentItem> loose = LooseItems(data);
        loose.Sort((a, b) =>
        {
            EquipmentDatabase.Item da = DB?.Find(a.id), db = DB?.Find(b.id);
            bool ta = TreasureRules.Is(da), tb = TreasureRules.Is(db);
            if (ta != tb) return ta ? -1 : 1;
            if (ta) { int la = TreasureRules.Level(a), lb = TreasureRules.Level(b); if (la != lb) return lb.CompareTo(la); }
            else if (a.rarity != b.rarity) return b.rarity.CompareTo(a.rarity);
            int sa = da != null ? SlotOrder[(int)da.slot] : 9, sb = db != null ? SlotOrder[(int)db.slot] : 9;
            if (sa != sb) return sa.CompareTo(sb);
            float ga = da != null ? da.grade : 0f, gb = db != null ? db.grade : 0f;
            if (!Mathf.Approximately(ga, gb)) return gb.CompareTo(ga);
            return a.uid.CompareTo(b.uid);
        });
        foreach (EquipmentItem item in loose) data.equipment.Remove(item);
        data.equipment.AddRange(loose);//已穿着的留在前面，背包格子按新顺序显示
        Changed?.Invoke();
        GameSession.instance?.NotifyDataChanged();
    }

    public static int SellPrice(EquipmentItem item)
    {
        EquipmentDatabase.Item def = item != null ? DB?.Find(item.id) : null;
        if (def == null) return 0;
        return TreasureRules.Is(def) ? TreasureRules.SellValue(TreasureRules.Level(item)) : EquipmentRules.SellValue(def.grade, item.rarity);
    }

    private static void MoveToEnd(SaveData data, EquipmentItem item)
    {
        if (data.equipment.Remove(item)) data.equipment.Add(item);
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
