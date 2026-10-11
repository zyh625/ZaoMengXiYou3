using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Map : MonoBehaviour
{
    private SaveData data;
    [SerializeField] private Button[] buttons;
    void Start()
    {
        data = MainMenu.instance.data;
        for(int i = 0; i < data.level; i++)
        {
            buttons[i].gameObject.SetActive(true);//开启已经通关的关卡和下一关
        }
    }
    public void Save()//保存游戏按键
    {
        SaveSystem.Save();
    }
    public void EnterLevel(int level)//进入游戏的按键
    {
        data.curLevel= level;
        string sceneName = "Level" + level.ToString() + "Scene";
        SceneManager.LoadScene(sceneName);
    }
}
