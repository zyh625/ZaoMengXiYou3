using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Defeat : MonoBehaviour
{
    [SerializeField] private Image[] times;
    [SerializeField] private Image[] process;
    private SaveData data;
    private void Start()
    {
        data = MainMenu.instance.data;
        SetTime();
        if (data.curLevel >= 10)
        {
            process[0].sprite = Font.GetSprite(data.curLevel / 10);
            process[0].gameObject.SetActive(true);
        }
        process[1].sprite = Font.GetSprite(data.curLevel % 10);
    }
    private void SetTime()
    {
        int t = data.costTime;
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
    public void BackToMap()//返回地图
    {
        SceneManager.LoadScene("MapScene");
    }
    public void TryAgain()//重新挑战
    {
        string sceneName = "Level" + data.curLevel.ToString() + "Scene";
        SceneManager.LoadScene(sceneName);
    }
}
