using UnityEngine;

public class MainMenu : MonoBehaviour
{
    public static MainMenu instance;
    public SaveData data;
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
            name = n
        };
        instance = this;
        DontDestroyOnLoad(gameObject);
    }
    
}
