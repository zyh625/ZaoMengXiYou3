using UnityEngine;

public class MainMenu : MonoBehaviour
{
    public static MainMenu instance;
    public SaveData data;
    public void Awake()
    {
        if (instance != null)
        {
            Destroy(instance);
            return;
        }
        data = SaveSystem.Load();
        data ??= new()
        {
            exp = 0f,
            playerLevel = 1,
            level = 1,
            curLevel = 0,
            playerState = 0,
            costTime = 0,
            mvp = 0
        };
        instance = this;
        DontDestroyOnLoad(gameObject);
    }
    
}
