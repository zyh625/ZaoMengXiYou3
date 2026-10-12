using UnityEngine.UI;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using System;

public class MenuButton : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI inputName;
    [SerializeField] TextMeshProUGUI[] fileInformantion;//存档信息
    [SerializeField] private GameObject CreatFile;//创建存档界面，输入昵称
    [SerializeField] private GameObject selectUI;//存档界面
    [SerializeField] private GameObject coverUI;//存档界面
    [SerializeField] private Button[] buttons;//新游戏开启第一个，继续游戏开启第二个
    [SerializeField] private Button[] fileButtons;//存档按钮
    private bool[] haveFile = new bool[6];
    private float alpha;
    private int fileIndex = -1;//选中存档
    void Start()
    {
        alpha = fileButtons[0].image.color.a;
        for(int i = 0; i < 6; i++)
        {
            Color c = fileButtons[i].image.color;
            c.a *= 0.8f;//开始都变淡
            fileButtons[i].image.color = c;
            SaveData data = SaveSystem.Load(i);
            if (data != null)
            {
                haveFile[i] = true;
                string s = data.name + "\n" + data.lastPlayYear + "/" + data.lastPlayMonth + "/" + data.lastPlayDay;
                fileInformantion[i].SetText(s);
            }
            else
            {
                haveFile[i] = false;
                fileInformantion[i].SetText("");
            }
        }
    }
    public void NewGame()//新游戏
    {
        selectUI.SetActive(true);
        TurnOn(true);
        buttons[0].gameObject.SetActive(true);
    }
    public void BackToMenu()//从存档界面返回主界面
    {
        buttons[0].gameObject.SetActive(false);
        buttons[1].gameObject.SetActive(false);
        selectUI.SetActive(false);
    }
    public void EnsureCover()//确定覆盖存档
    {
        if (fileIndex == -1) return;
        if (!haveFile[fileIndex])//如果本来为空，直接创建存档
            CreatFile.gameObject.SetActive(true);
        else
            coverUI.SetActive(true);
    }
    public void BackToSelect()//从覆盖界面返回存档界面
    {
        coverUI.SetActive(false);
    }
    public void ContinueGame()//点击继续游戏
    {
        selectUI.SetActive(true);
        TurnOn(false);
        buttons[1].gameObject.SetActive(true);
    }
    public void ClickFile(int num)
    {
        Debug.Log(num);
        if (fileIndex == -1)
        {
            Color c = fileButtons[num].image.color;
            c.a = alpha;
            fileButtons[num].image.color = c;
        }
        else if (fileIndex == num)
        {
            EnterFile();
        }
        else
        {
            Color c = fileButtons[fileIndex].image.color;
            c.a = fileButtons[num].image.color.a;
            fileButtons[fileIndex].image.color = c;
            c = fileButtons[num].image.color;
            c.a= alpha;
            fileButtons[num].image.color = c;
        }
        fileIndex = num;
    }
    public void EnterFile()//确定进入存档
    {
        if (fileIndex == -1) return;
        if (haveFile[fileIndex])//已创建的
            StartGame();
        else//新创建
            CreatFile.SetActive(true);
    }
    public void CoverFile()//确定覆盖存档了
    {
        fileButtons[fileIndex].gameObject.SetActive(false);
        SaveSystem.Delete(fileIndex);
        StartGame();
    }
    public void AboutGame()//关于游戏
    {

    }
    public  void ExitGame()//退出游戏
    {
        SaveSystem.Save();
        Application.Quit();
    }
    public void StartGame()
    {
        if (!haveFile[fileIndex])
        {
            name = inputName.text;
            if (name.Length == 0 || name.Length > 12)
            {
                inputName.SetText("Your name's length should be in range[1,12]!");
                return;
            }
            else if (CheckName(name))
            {
                inputName.SetText("You have used this name! Fuck you.");
                return;
            }
            MainMenu.instance.SelectFile(fileIndex, name);
        }
        else 
            MainMenu.instance.SelectFile(fileIndex, "");
        SceneManager.LoadScene("MapScene");
    }
    private void TurnOn(bool b)
    {
        for(int i = 0; i < 6; i++)
            fileButtons[i].gameObject.SetActive(b);
    }
    private bool CheckName(string name)
    {
        for(int i = 0; i < 6; i++)
        {
            if (haveFile[i] && SaveSystem.Load(i).name == name) return true;
        }
        return false;
    }
}
