using System;
using UnityEngine;
[Serializable]
public class SaveData
{
    public float exp;//玩家经验值
    public int playerLevel;//玩家等级
    public int level;//最高可进入的关卡
    public int curLevel;//当前进入的关卡,从1开始
    public int playerState;//玩家过关状态，百分数
    public int costTime;//过关耗时
    public int mvp;//本关综合效果最强的角色，猴、沙、猪、唐
    public int file;//当前使用的存档，0-5
    public string name;//存档名字,最多12个字母
    public string date;//最近一次游玩
    public string priScene;//刚才的场景
}
