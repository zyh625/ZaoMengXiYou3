using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerButtonManager : MonoBehaviour
{
    [SerializeField] private GameObject bag, settings, skills;
    public void OnBag() => OpenPanel(bag);
    public void OnSettings() => OpenPanel(settings);
    public void OnSkills() => OpenPanel(skills);

    private void OpenPanel(GameObject panel)//同一时刻只显示一个暂停菜单，避免关闭一个后仍有另一个覆盖游戏
    {
        if (panel == null) return;
        if (bag != null && bag != panel) bag.SetActive(false);
        if (settings != null && settings != panel) settings.SetActive(false);
        if (skills != null && skills != panel) skills.SetActive(false);
        GamePause.Pause();//统一暂停游戏时间和角色输入，界面的鼠标操作继续可用
        panel.SetActive(true);
    }

    public void BackGame()//设置中的继续游戏只关闭菜单，不改变会话或存档
    {
        ClosePanels();
        GamePause.Resume();
    }

    public void BackMap()//返回地图保留GameSession中的临时金币、技能与经验，不触发保存
    {
        ClosePanels();
        BattleRun.Cancel();
        GoldManager.Current?.EndRun();//未完成吸附的金币不能在离开战斗后补入账
        GamePause.Reset();
        SceneManager.LoadScene("MapScene");
    }

    public void BackMenu()//主动退出到主菜单时丢弃本次未保存数据，下一次仍从上次存档继续
    {
        ClosePanels();
        BattleRun.Cancel();
        GoldManager.Current?.EndRun();
        GameSession.instance?.DiscardUnsavedProgress();
        GamePause.Reset();
        SceneManager.LoadScene("MenuScene");
    }

    private void ClosePanels()//恢复或切换场景时同时移除菜单遮挡，保持按钮已有的布局和引用
    {
        if (bag != null) bag.SetActive(false);
        if (settings != null) settings.SetActive(false);
        if (skills != null) skills.SetActive(false);
    }
}
