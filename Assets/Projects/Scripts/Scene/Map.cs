using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Map : MonoBehaviour
{
    private SaveData data;
    [SerializeField] private Button[] buttons;
    [SerializeField] private Image saveSucess;
    [SerializeField] private float saveDuration = 2f;//"保存游戏逐渐消失"
    private bool isSaving = false;
    private float beginSave = 0f;
    private void Start()
    {
        data = MainMenu.instance.data;
        for(int i = 0; i < data.level; i++)
        {
            buttons[i].gameObject.SetActive(true);//开启已经通关的关卡和下一关
        }
    }
    private void Update()
    {
        if (isSaving)
        {
            ;
        }
    }
    public void Save()//保存游戏按键
    {
        SaveSystem.Save();
        saveSucess.gameObject.SetActive(true);
        isSaving = true;
        beginSave = Time.time;
    }
    public void EnterLevel(int level)//进入游戏的按键
    {
        data.curLevel= level;
        string sceneName = "Level" + level.ToString() + "Scene";
        SceneManager.LoadScene(sceneName);
    }
}
