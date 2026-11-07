using System;
using System.Collections.Generic;
using UnityEngine;

//技能键（U、I、O）和换人键（1～4）各绑定到哪个技能 / 哪个角色。
//只改“按键 → 目标”的映射：同一个技能绑到多个键仍是同一个技能、共用冷却；多个数字键可以绑同一个角色。
//保存在本机设置里，改完立即生效，不随存档的未保存进度一起丢弃。
public static class KeyBindings
{
    public const int SkillKeys = 3, SwitchKeys = 4;
    public static readonly string[] SkillKeyNames = { "U", "I", "O" };
    private const string SkillPrefix = "KeyBind.Skill.", SwitchKey = "KeyBind.Switch";
    public static event Action Changed;

    private static int[][] skills;//[角色][按键] = 技能编号1～3
    private static PlayerId[] switches;
    private static IReadOnlyList<PlayerId> switchView;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache() { skills = null; switches = null; switchView = null; Changed = null; }

    private static void Load()
    {
        if (skills != null) return;
        skills = new int[4][];
        for (int id = 0; id < 4; id++)
        {
            skills[id] = new[] { 1, 2, 3 };
            string saved = PlayerPrefs.GetString(SkillPrefix + id, "");
            for (int k = 0; k < SkillKeys && k < saved.Length; k++)
                if (saved[k] >= '1' && saved[k] <= '3') skills[id][k] = saved[k] - '0';
        }
        switches = new[] { PlayerId.WuKong, PlayerId.BaJie, PlayerId.Sha, PlayerId.Tang };
        string order = PlayerPrefs.GetString(SwitchKey, PlayerPrefs.GetString("CharacterSwitchOrder", ""));//兼容原“调整顺序”保存的顺序
        for (int i = 0; i < SwitchKeys && i < order.Length; i++)
            if (order[i] >= '0' && order[i] <= '3') switches[i] = (PlayerId)(order[i] - '0');
    }

    public static int SkillFor(PlayerId id, int key)//key 从1开始；返回技能编号1～3
    {
        Load();
        return key >= 1 && key <= SkillKeys && (int)id >= 0 && (int)id < 4 ? skills[(int)id][key - 1] : key;
    }

    public static PlayerId SwitchTarget(int slot)//slot 从0开始
    {
        Load();
        return switches[Mathf.Clamp(slot, 0, SwitchKeys - 1)];
    }

    public static IReadOnlyList<PlayerId> SwitchTargets { get { Load(); return switchView ??= Array.AsReadOnly(switches); } }

    public static void SetSkill(PlayerId id, int key, int skill)
    {
        Load();
        if (key < 1 || key > SkillKeys || skill < 1 || skill > 3) return;
        skills[(int)id][key - 1] = skill;
        PlayerPrefs.SetString(SkillPrefix + (int)id, string.Concat(skills[(int)id]));
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    public static void SetSwitch(int slot, PlayerId target)
    {
        Load();
        if (slot < 0 || slot >= SwitchKeys) return;
        switches[slot] = target;
        var codes = new char[SwitchKeys];
        for (int i = 0; i < SwitchKeys; i++) codes[i] = (char)('0' + (int)switches[i]);
        PlayerPrefs.SetString(SwitchKey, new string(codes));
        PlayerPrefs.Save();
        Changed?.Invoke();
    }
}
