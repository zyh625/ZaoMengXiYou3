using System;
using System.Collections.Generic;
using System.Text;
public enum PlayerId
{
    WuKong=0,
    BaJie=1,
    Sha=2,
    Tang=3
}
[Serializable]
public class PlayerData
{
    public PlayerId id;
    public int playerLevel = 1;
    public float exp = 0f;
    public int[] skillLevels = new int[3];//技能等级

    public void Normalize()//兼容旧存档未填写的角色属性，技能数组只扩充、不截断以后新增的技能
    {
        playerLevel = Math.Max(1, playerLevel);
        if (float.IsNaN(exp) || float.IsInfinity(exp) || exp < 0f) exp = 0f;
        if (skillLevels == null) skillLevels = new int[3];
        else if (skillLevels.Length < 3) Array.Resize(ref skillLevels, 3);
        for (int i = 0; i < skillLevels.Length; i++) skillLevels[i] = Math.Max(0, skillLevels[i]);
    }

    public int GetSkillLevel(int skillNumber)//技能编号与战斗脚本一致，从1开始，0级表示尚未学习
    {
        Normalize();
        return skillNumber > 0 && skillNumber <= skillLevels.Length ? skillLevels[skillNumber - 1] : 0;
    }

    public void SetSkillLevel(int skillNumber, int level)//奖励或以后新增技能可扩充数据，不与固定三个输入槽绑死
    {
        if (skillNumber < 1) return;
        Normalize();
        if (skillNumber > skillLevels.Length) Array.Resize(ref skillLevels, skillNumber);
        skillLevels[skillNumber - 1] = Math.Max(0, level);
    }
}
[Serializable]
public class SaveData
{
    public const int InitialGold = 10000;//只在新建存档时发放，读取旧档不会覆盖已有金币
    public const int MaxRebirth = 81;
    public List<PlayerData> players = new();
    public int gameLevel = 1;//最高可进入的关卡
    public int rebirth = 0;//完成第9关后自愿再开一轮；旧JSON缺少此字段时为0。
    public bool rebirthAvailable;//本轮第9关胜利后获得，延后选择可跨会话保留；成功轮回后消耗。
    public int unlocked = -1;//角色位掩码；-1兼容旧档，Normalize按已解锁关卡与已有养成补全。
    public bool IsPlayerUnlocked(PlayerId id) => Enum.IsDefined(typeof(PlayerId), id) && ((unlocked < 0 ? 1 : unlocked) & (1 << (int)id)) != 0;
    public void UnlockAfterVictory(int level)
    {
        PlayerId? joined = level == 1 ? PlayerId.BaJie : level == 2 ? PlayerId.Tang : level == 3 ? PlayerId.Sha : null;
        if (joined.HasValue) unlocked = Math.Max(0, unlocked) | (1 << (int)joined.Value) | 1;
    }
    public string name;//存档名字,最多12个字母
    public string date;//最近一次游玩
    public int gold;//已经完整拾取入账的金币，地上和吸附中的金币不写进这里
    public List<int> defeatedBossLevels = new();//按关卡记录Boss首杀，重新挑战不会重复获得首杀掉落
    public PlayerData GetPlayer(PlayerId id)
    {
        if (!Enum.IsDefined(typeof(PlayerId), id)) return null;
        if (players == null) players = new List<PlayerData>();
        PlayerData player = players.Find(candidate => candidate != null && candidate.id == id);
        if (player == null)
        {
            player = new PlayerData { id = id };
            players.Add(player);
        }
        player.Normalize();
        return player;
    }

    public void Normalize()//读取旧JSON后补齐四角色和新增字段，不改动原本有效的等级、技能与金币
    {
        gameLevel = Math.Max(1, gameLevel);
        rebirth = Math.Clamp(rebirth, 0, MaxRebirth);
        gold = Math.Max(0, gold);
        name = RepairName(name ?? string.Empty);
        date = date ?? string.Empty;
        if (players == null) players = new List<PlayerData>();
        players.RemoveAll(player => player == null);
        foreach (PlayerData player in players) player.Normalize();
        foreach (PlayerId id in Enum.GetValues(typeof(PlayerId))) GetPlayer(id);
        int joined = 1;
        if (gameLevel >= 2) joined |= 1 << (int)PlayerId.BaJie;
        if (gameLevel >= 3) joined |= 1 << (int)PlayerId.Tang;
        if (gameLevel >= 4 || rebirth > 0) joined = 15;
        foreach (PlayerData player in players)//不因新的解锁顺序丢掉老档已经培养的角色。
            if (player.playerLevel > 1 || player.exp > 0f || Array.Exists(player.skillLevels, level => level > 0)) joined |= 1 << (int)player.id;
        unlocked = (Math.Max(0, unlocked) | joined) & 15;
        if (defeatedBossLevels == null) defeatedBossLevels = new List<int>();
        HashSet<int> seen = new();//只在读盘或写盘前整理，掉落时直接读取记录
        defeatedBossLevels.RemoveAll(level => level < 1 || !seen.Add(level));
    }

    //旧版本在中文系统上可能把 UTF-8 名字按 Latin-1 解码后再次保存；这里在读取时还原一次。
    private static string RepairName(string value)
    {
        if (string.IsNullOrEmpty(value) || HasCjk(value)) return value;
        try
        {
            byte[] bytes = Encoding.GetEncoding("ISO-8859-1").GetBytes(value);
            string decoded = Encoding.UTF8.GetString(bytes);
            if (decoded != value && !decoded.Contains('\uFFFD') && HasCjk(decoded)) return decoded;
        }
        catch (Exception) { }
        return value;
    }

    private static bool HasCjk(string value)
    {
        foreach (char c in value)
            if (c >= '\u4E00' && c <= '\u9FFF') return true;
        return false;
    }

    public bool HasDefeatedBoss(int level) => level > 0 && defeatedBossLevels != null && defeatedBossLevels.Contains(level);

    public bool MarkBossDefeated(int level)//只在首次击杀返回true，掉落和结算可以安全地重复检查
    {
        if (level < 1 || HasDefeatedBoss(level)) return false;
        if (defeatedBossLevels == null) defeatedBossLevels = new List<int>();
        defeatedBossLevels.Add(level);
        return true;
    }

    public void AddGold(int amount)//拾取完成才调用，使用long避免极大金额相加后溢出变负
    {
        if (amount <= 0) return;
        gold = (int)Math.Min(int.MaxValue, (long)Math.Max(0, gold) + amount);
    }

    public bool TrySpendGold(int amount)//扣费与学习在同一入口完成，无钱和非法负价格都不能购买
    {
        if (amount < 0 || gold < amount) return false;
        gold -= amount;
        return true;
    }
    public static SaveData CreatNew(string name)
    {
        var data = new SaveData { name = name, gold = InitialGold };
        foreach(PlayerId id in Enum.GetValues(typeof(PlayerId)))
        {
            data.players.Add(new PlayerData { id = id });
        }
        data.Normalize();
        return data;
    }
}
