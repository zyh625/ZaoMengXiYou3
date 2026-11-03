using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class VictoryControl : MonoBehaviour
{
    [SerializeField] private Image[] times;//六个数字，时分秒
    [SerializeField] private Image[] states;//原有十位、个位，保留布局
    [SerializeField] private Image stateHundreds;//只在100%时显示新增百位，不挪动原有两位数字
    [SerializeField] private Image head;//谁是MVP展示谁
    [SerializeField] private Sprite[] heads;//师徒4人的头像
    [SerializeField] private Button rebirthButton, nextLevelButton;
    [SerializeField] private Text rebirthMessage;
    private bool enteringRebirth;
    public bool CanRebirth => result != null && result.victory && result.curLevel == 9;
    private GameSession instance;
    private BattleData result;//优先使用切场景前冻结的临时本轮结果，直接打开界面也不产生空引用
    private void Start()
    {
        instance = GameSession.instance;
        if (BattleRun.IsRunning) BattleRun.Finish(true);
        result = (instance != null ? instance.battle : null) ?? BattleRun.LastResult ?? new BattleData();//兼容第一关旧流程直接写battle，不能误用上次其他关卡的静态结果
        if (instance != null && instance.data != null && result.curLevel > 0 && result.victory)
        {
            instance.RegisterVictory(result);
        }
        SaveSystem.Save();//有已选存档时胜利自动写盘，无选档直接运行时保存系统不会覆盖旧文件
        SetTime();
        SetState();
        SetMVP();
        if (rebirthButton != null)
        {
            rebirthButton.gameObject.SetActive(CanRebirth);
            rebirthButton.interactable = CanRebirth && instance != null && instance.HasSelectedFile;
        }
        if (nextLevelButton != null) nextLevelButton.gameObject.SetActive(result.curLevel != 9);
        if (rebirthMessage != null)
        {
            bool joined = result.victory && result.curLevel >= 1 && result.curLevel <= 3;
            rebirthMessage.gameObject.SetActive(CanRebirth || joined);
            rebirthMessage.text = instance != null && instance.HasSelectedFile
                ? $"已通关第9关，再入轮回？保留能力与金币，从第1关重新开始；也可稍后在地图选择。当前第 {instance.Rebirth} 轮回"
                : "第 0 轮回 · 请先在主菜单选择存档";
            if (joined)
            {
                string companion = result.curLevel == 1 ? "猪八戒" : result.curLevel == 2 ? "唐僧" : "沙僧";
                rebirthMessage.text = "伙伴已解锁：" + companion + "（Q/E 切换）";
            }
        }
    }
    private void SetTime()
    {
        BattleResultDisplay.SetTime(times, result.costTime);
    }
    private void SetState()
    {
        BattleResultDisplay.SetPercent(states, stateHundreds, result.playerState);
    }
    private void SetMVP()
    {
        BattleResultDisplay.SetMVP(head, heads, result.mvp);
    }
    public void BackToMap()
    {
        if (instance != null) instance.priScene = "VictoryScene";
        SceneManager.LoadScene(instance != null ? "MapScene" : "MenuScene");
    }
    public void NextLevel()
    {
        if (result == null || result.curLevel == 9) { BackToMap(); return; }
        int next = Mathf.Max(1, result.curLevel + 1);
        string sceneName = "Level" + next.ToString() + "Scene";
        if (!Application.CanStreamedLevelBeLoaded(sceneName)) { BackToMap(); return; }
        if (instance != null)
        {
            instance.battle = new BattleData { curLevel = next };//不把已展示的上一轮统计当作下一轮可写数据
            instance.priScene = "VictoryScene";
        }
        SceneManager.LoadScene(sceneName);
    }
    public void EnterRebirth()
    {
        if (enteringRebirth || !CanRebirth || instance == null) return;
        if (!Application.CanStreamedLevelBeLoaded("Level1Scene"))
        {
            if (rebirthMessage != null) rebirthMessage.text = "第1关未加入构建列表，暂时无法进入";
            return;
        }
        enteringRebirth = true;
        if (!instance.TryBeginRebirth(result))
        {
            enteringRebirth = false;
            if (rebirthMessage != null) rebirthMessage.text = "保存失败，请检查存档路径后重试";
            return;
        }
        if (rebirthButton != null) rebirthButton.interactable = false;
        SceneManager.LoadScene("Level1Scene");
    }
}
