using System;
using UnityEngine;
public class BattleData
{
    public int curLevel;//当前进入的关卡,从1开始
    public int costTime;//过关耗时
    public int mvp;//本关的mvp
    public int playerState ;//总体平均过关状态
    public int progress;//本轮失败闯关百分数，只用于结算，不进入SaveData
    public bool victory;//本轮结果，只在当前进程传递给结算界面
}

public class GameSession : MonoBehaviour
{
    public static GameSession instance;
    public BattleData battle = new();
    public int curFile = -1;//当前使用的存档，未经过选档流程时不关联任何磁盘槽
    public string priScene;//上一个场景
    public int maxLevel;

    public SaveData data;
    public event Action DataChanged;//金币或技能购买真正发生变化后，当前打开的技能界面统一刷新
    private bool fileSelected;//必须真实调用SelectFile才允许写盘，序列化的curFile值不能当成选档授权
    public bool HasSelectedFile => fileSelected && curFile >= 0;
    public void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
    }
    public void SelectFile(int num,string n)//选中存档进入
    {
        if (instance != null&&instance!=this)
        {
            instance.SelectFile(num, n);//菜单再次载入时仍由原本跨场景会话接收选择
            return;
        }
        if (num < 0) return;
        curFile = num;
        data = SaveSystem.Load(num) ?? SaveData.CreatNew(n);
        data.Normalize();
        battle = new BattleData();//切换存档不能沿用另一个档的临时结算
        BattleRun.Cancel();
        fileSelected = true;
        instance = this;
        maxLevel = data.gameLevel;
        DontDestroyOnLoad(gameObject);
        NotifyDataChanged();//选择另一份存档后，已显示的数据也跟随当前存档切换
    }

    public static GameSession EnsureSession()//直接运行战斗场景也有完整内存数据，但不会创建或覆盖任何存档文件
    {
        if (instance == null) new GameObject("GameSession").AddComponent<GameSession>();
        instance.EnsureRuntimeData();
        return instance;
    }

    public SaveData EnsureRuntimeData()
    {
        if (data == null) data = SaveData.CreatNew(string.Empty);
        data.Normalize();
        if (battle == null) battle = new BattleData();
        maxLevel = data.gameLevel;
        return data;
    }

    public void ClearSelectedFile()//明确删除当前槽后，旧会话不能通过自动保存把该槽重新写回来
    {
        fileSelected = false;
        curFile = -1;
    }

    public bool TryLearnSkill(PlayerId id, int skillNumber, int cost)//保留旧入口，实际价格统一读取等级配置，避免界面传入过期价格
        => TryLearnSkill(id, skillNumber);

    public bool TryLearnSkill(PlayerId id, int skillNumber)//技能编号从1开始，学习与金币扣费要么一起成功、要么都不变
    {
        SaveData save = EnsureRuntimeData();
        SkillProgression progression = SkillProgression.Current;
        if (!progression.HasSkill(id, skillNumber)) return false;
        PlayerData player = save.GetPlayer(id);
        int cost = progression.GetLevelData(id, skillNumber, 1).goldCost;//第一等级的价格就是学习费用
        if (player == null || player.GetSkillLevel(skillNumber) != 0 || !save.TrySpendGold(cost)) return false;
        player.SetSkillLevel(skillNumber, 1);
        SaveSystem.Save();
        NotifyDataChanged();
        return true;
    }

    public bool TryUpgradeSkill(PlayerId id, int skillNumber, int cost)//兼容已有按钮调用，升级价格以目标等级配置为准
        => TryUpgradeSkill(id, skillNumber);

    public bool TryUpgradeSkill(PlayerId id, int skillNumber)//未学习或已经满级时不会扣费，也不会发出数据更新通知
    {
        SaveData save = EnsureRuntimeData();
        SkillProgression progression = SkillProgression.Current;
        if (!progression.HasSkill(id, skillNumber)) return false;
        PlayerData player = save.GetPlayer(id);
        if (player == null) return false;
        int level = player.GetSkillLevel(skillNumber);
        if (level < 1 || level >= progression.MaxLevel) return false;
        int cost = progression.GetLevelData(id, skillNumber, level + 1).goldCost;//升级按钮展示并扣除即将升到的那一级的价格
        if (!save.TrySpendGold(cost)) return false;
        player.SetSkillLevel(skillNumber, level + 1);
        SaveSystem.Save();
        NotifyDataChanged();
        return true;
    }

    public void NotifyDataChanged() => DataChanged?.Invoke();//拾取金币等其他系统在真实变更当前存档数据后调用

    public void UnlockLevel(int level)//解锁只会上升，失败和重玩低关卡都不会降低已有进度
    {
        SaveData save = EnsureRuntimeData();
        save.gameLevel = Math.Max(save.gameLevel, Math.Max(1, level));
        maxLevel = save.gameLevel;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
    
}
