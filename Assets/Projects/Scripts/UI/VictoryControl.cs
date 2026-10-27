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
    private GameSession instance;
    private BattleData result;//优先使用切场景前冻结的临时本轮结果，直接打开界面也不产生空引用
    private void Start()
    {
        instance = GameSession.instance;
        if (BattleRun.IsRunning) BattleRun.Finish(true);
        result = (instance != null ? instance.battle : null) ?? BattleRun.LastResult ?? new BattleData();//兼容第一关旧流程直接写battle，不能误用上次其他关卡的静态结果
        if (instance != null && instance.data != null && result.curLevel > 0)
        {
            instance.data.gameLevel = Mathf.Max(instance.data.gameLevel, result.curLevel + 1);//失败不解锁，胜利只提高最高可进入关卡
            instance.maxLevel = instance.data.gameLevel;
        }
        SaveSystem.Save();//有已选存档时胜利自动写盘，无选档直接运行时保存系统不会覆盖旧文件
        SetTime();
        SetState();
        SetMVP();
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
}
