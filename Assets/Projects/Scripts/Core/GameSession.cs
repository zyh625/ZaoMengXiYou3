using System;
using UnityEngine;

public class GameSession : MonoBehaviour
{
    public static GameSession instance;
    public string priScene;
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
        data = SaveSystem.Load(num);
        data ??= new()
        {
            exp = 0f,
            playerLevel = 1,
            level = 1,
            curLevel = 0,
            playerState = 0,
            costTime = 0,
            mvp = 0,
            file = num,
            name = n,
            date = DateTime.Now.ToString("yyyy-MM-dd")
        };
        instance = this;
        DontDestroyOnLoad(gameObject);
    }
    
}
