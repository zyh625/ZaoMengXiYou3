using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct SkillLevelData
{
    [Min(0)] public int goldCost;//购买这一等级所需金币；第一项为学习费用，其后为升级费用
    [Min(0)] public int manaCost;//技能处于这一等级时，每次成功释放的蓝量消耗
    [Min(0)] public int cooldown;//技能处于这一等级时，每次成功释放的总冷却秒数，沿用界面的整数数字

    public SkillLevelData(int goldCost, int manaCost, int cooldown)
    {
        this.goldCost = goldCost;
        this.manaCost = manaCost;
        this.cooldown = cooldown;
    }
}

//技能界面和战斗共用同一份等级配置，存档只保存已学习的等级，不复制这些可调整数值。
[CreateAssetMenu(fileName = "SkillProgression", menuName = "造梦西游/技能等级配置")]
public class SkillProgression : ScriptableObject
{
    [Serializable]
    public sealed class SkillDefinition
    {
        public PlayerId player;//角色编号沿用存档中的PlayerId
        [Min(1)] public int skillNumber = 1;//技能编号从1开始，与Combat中的Skill1、Skill2对应
        public SkillLevelData[] levels = CreateDefaultLevels();//数组下标0对应1级，下标1对应2级，新技能同样填入默认价格与属性

        private static SkillLevelData[] CreateDefaultLevels()//从Inspector新增技能项时也不会得到意外的零费用和零冷却
        {
            SkillLevelData[] result = new SkillLevelData[6];
            for (int i = 0; i < result.Length; i++) result[i] = new SkillLevelData(100, 10, 10);
            return result;
        }
    }

    private const int DefaultMaxLevel = 6;//没有配置资源时仍使用统一默认上限
    private static SkillProgression current;//全项目共用Resources中的配置，不逐角色复制配置实例
    [SerializeField, Min(1)] private int maxLevel = DefaultMaxLevel;//提高上限后在各技能的levels中补上对应等级数据
    [SerializeField] private SkillDefinition[] skills = Array.Empty<SkillDefinition>();//十二个现有技能分别配置，以后也可继续添加
    private readonly Dictionary<(PlayerId player, int skillNumber), SkillDefinition> definitions = new();//按角色和编号缓存，运行时不反复扫描全部配置
    private bool initialized;//只在首次读取或编辑配置后重建索引

    public static SkillProgression Current
    {
        get
        {
            if (current == null)
            {
                current = Resources.Load<SkillProgression>("Skills/SkillProgression");
                if (current == null)
                {
                    current = CreateInstance<SkillProgression>();//直接运行场景且资源缺失时也有默认费用和属性
                    current.hideFlags = HideFlags.HideAndDontSave;
                }
            }
            return current;
        }
    }

    public int MaxLevel => Mathf.Max(1, maxLevel);

    public bool HasSkill(PlayerId player, int skillNumber)//购买入口验证技能定义，新增第四招时无需改存档数组长度判断
    {
        if (!Enum.IsDefined(typeof(PlayerId), player) || skillNumber < 1) return false;
        BuildDefinitions();
        return definitions.ContainsKey((player, skillNumber)) || definitions.Count == 0 && skillNumber <= 3;
    }

    public SkillLevelData GetLevelData(PlayerId player, int skillNumber, int level)
    {
        BuildDefinitions();
        if (definitions.TryGetValue((player, skillNumber), out SkillDefinition definition)
            && definition.levels != null && definition.levels.Length > 0)
        {
            //未学习时展示1级属性；扩展上限但尚未补齐数组时沿用最后一个已配置等级。
            int index = Mathf.Clamp(level, 1, MaxLevel) - 1;
            SkillLevelData data = definition.levels[Mathf.Min(index, definition.levels.Length - 1)];
            data.goldCost = Mathf.Max(0, data.goldCost);
            data.manaCost = Mathf.Max(0, data.manaCost);
            data.cooldown = Mathf.Max(0, data.cooldown);
            return data;
        }
        return new SkillLevelData(100, 10, 10);
    }

    private void BuildDefinitions()//空项或重复编号不让运行时读取出现异常，重复项沿用最先填写的那一项
    {
        if (initialized) return;
        initialized = true;
        definitions.Clear();
        if (skills == null) return;
        foreach (SkillDefinition definition in skills)
        {
            if (definition == null || definition.skillNumber < 1 || !Enum.IsDefined(typeof(PlayerId), definition.player)) continue;
            definitions.TryAdd((definition.player, definition.skillNumber), definition);
        }
    }

    private void OnEnable() => initialized = false;
    private void OnValidate() => initialized = false;//Inspector修改后，界面下一次刷新与下一次施法会读取新的等级数据
}
