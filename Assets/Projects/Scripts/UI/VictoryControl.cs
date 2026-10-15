using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class VictoryControl : MonoBehaviour
{
    [SerializeField] private Image[] times;//六个数字，时分秒
    [SerializeField] private Image[] states;//最多两个数字，过关状态百分之多少
    [SerializeField] private Image head;//谁是MVP展示谁
    [SerializeField] private Sprite[] heads;//师徒4人的头像
    private GameSession instance;
    private void Start()
    {
        SaveSystem.Save();//每次胜利都自动保存
        instance = GameSession.instance;
        SetTime();
        SetState();
        SetMVP();
    }
    private void SetTime()
    {
        int t = instance.battle.costTime;
        int h = Mathf.Min(99, t / 3600);//最大99小时
        int m = (t % 3600) / 60;
        int s = t % 60;
        times[0].sprite = Font.GetSprite(h / 10);
        times[1].sprite = Font.GetSprite(h % 10);
        times[2].sprite = Font.GetSprite(m / 10);
        times[3].sprite = Font.GetSprite(m % 10);
        times[4].sprite = Font.GetSprite(s / 10);
        times[5].sprite = Font.GetSprite(s % 10);
    }
    private void SetState()
    {
        states[0].sprite = Font.GetSprite(instance.battle.playerState / 10);
        states[1].sprite = Font.GetSprite(instance.battle.playerState % 10);
        if (instance.battle.playerState >= 10) states[0].gameObject.SetActive(true);
    }
    private void SetMVP()
    {
        head.sprite = heads[instance.battle.mvp];
    }
    public void BackToMap()
    {
        GameSession.instance.priScene = "VictoryScene";
        SceneManager.LoadScene("MapScene");
    }
    public void NextLevel()
    {
        instance.battle.curLevel++;
        string sceneName = "Level" + instance.battle.curLevel.ToString() + "Scene";
        GameSession.instance.priScene = "VictoryScene";
        SceneManager.LoadScene(sceneName);
    }
}
