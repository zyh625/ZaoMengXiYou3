using System;
using UnityEngine;
public class BattleData
{
    public int curLevel;//当前进入的关卡,从1开始
    public int costTime;//过关耗时
    public int mvp;//本关的mvp
    public int playerState ;//总体平均过关状态
}

public class GameSession : MonoBehaviour
{
    public static GameSession instance;
    public BattleData battle = new();
    public int curFile;//当前使用的存档
    public string priScene;//上一个场景
    public int maxLevel;

    public SaveData data;
    public void Awake()
    {
        instance = instance != null ? instance : this;
    }
    public void SelectFile(int num,string n)//选中存档进入
    {
        if (instance != null&&instance!=this)
        {
            Destroy(gameObject);
            return;
        }
        curFile = num;
        data = SaveSystem.Load(num) ?? SaveData.CreatNew(n);
        instance = this;
        maxLevel = data.gameLevel;
        DontDestroyOnLoad(gameObject);
    }
    
}
