using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Map : MonoBehaviour
{
    private SaveData data;
    private float beginAlpha;//“保存成功”的字体初始透明度
    [SerializeField] private Button[] buttons;
    [SerializeField] private Image saveSucess;
    [SerializeField] private float saveDuration = 2f;//"保存游戏"逐渐消失
    [SerializeField] private float saveKeep = 2f;//"保存游戏"先一直显示
    [SerializeField] private GameObject handBook, skills;
    private bool isSaving = false;
    private float beginSave = 0f;
    private GameSession instance;
    private void Start()
    {
        beginAlpha = saveSucess.color.a;
        instance = GameSession.EnsureSession();
        for(int i = 0; buttons != null && i < Mathf.Min(instance.maxLevel, buttons.Length); i++)
        {
            buttons[i].gameObject.SetActive(true);//开启已经通关的关卡和下一关
        }
    }
    private void Update()
    {
        if (isSaving&&Time.time-beginSave>=saveKeep)
        {
            float alpha = 1f - (Time.time - beginSave-saveKeep) / saveDuration;
            Color c = saveSucess.color;
            if (alpha <=0f)
            {
                isSaving = false;
                c.a = beginAlpha;
                saveSucess.gameObject.SetActive(false);
                saveSucess.color = c;
                return;
            }
            c.a = alpha;
            saveSucess.color = c;
        }
    }
    public void Skills() => skills.SetActive(true);
    public void HandBook()=>handBook.SetActive(true);
    public void BackToMap()=>handBook.SetActive(false);
    public void Back()//返回主菜单按键，不保存
    {
        instance.DiscardUnsavedProgress();//与设置中的返回主菜单一致，放弃未手动保存的本次修改
        SceneManager.LoadScene("MenuScene");
    }
    public void Save()//保存游戏按键
    {
        if (!SaveSystem.TrySave()) return;//只有真正写盘成功才显示保存成功，未选档直跑不误报
        saveSucess.gameObject.SetActive(true);
        isSaving = true;
        beginSave = Time.time;
    }
    public void EnterLevel(int level)//进入游戏的按键
    {
        instance.battle.curLevel= level;
        string sceneName = "Level" + level.ToString() + "Scene";
        instance.priScene = "MapScene";
        SceneManager.LoadScene(sceneName);
    }
}
