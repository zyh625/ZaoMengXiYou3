using System;
using System.Collections.Generic;
using UnityEditor.U2D.Animation;
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
}
[Serializable]
public class SaveData
{
    public List<PlayerData> players = new();
    public int gameLevel = 1;//最高可进入的关卡
    public string name;//存档名字,最多12个字母
    public string date;//最近一次游玩
    public PlayerData GetPlayer(PlayerId id)
    {
        return players.Find(player => player.id == id);
    }
    public static SaveData CreatNew(string name)
    {
        var data = new SaveData { name = name };
        foreach(PlayerId id in Enum.GetValues(typeof(PlayerId)))
        {
            data.players.Add(new PlayerData { id = id });
        }
        return data;
    }
}
