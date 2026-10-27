using System;
using System.Collections.Generic;
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
    public List<PlayerData> players = new();
    public int gameLevel = 1;//最高可进入的关卡
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
        gold = Math.Max(0, gold);
        name = name ?? string.Empty;
        date = date ?? string.Empty;
        if (players == null) players = new List<PlayerData>();
        players.RemoveAll(player => player == null);
        foreach (PlayerData player in players) player.Normalize();
        foreach (PlayerId id in Enum.GetValues(typeof(PlayerId))) GetPlayer(id);
        if (defeatedBossLevels == null) defeatedBossLevels = new List<int>();
        HashSet<int> seen = new();//只在读盘或写盘前整理，掉落时直接读取记录
        defeatedBossLevels.RemoveAll(level => level < 1 || !seen.Add(level));
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
