using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Defeat : MonoBehaviour
{
    [SerializeField] private Image[] times;
    [SerializeField] private Image[] process;
    [SerializeField] private Image processHundreds;//进度达到100%时额外显示百位，原有十位、个位不移动
    [SerializeField] private Image head;//失败界面的FMVP沿用Canvas现有MVP图片对象
    [SerializeField] private Sprite[] heads;//按悟空、八戒、沙僧、唐僧编号填写各自stand
    private GameSession instance;
    private BattleData result;//失败耗时与胜利读取同一份已停止统计的临时结果
    private void Start()
    {
        instance = GameSession.instance;
        if (BattleRun.IsRunning) BattleRun.Finish(false);
        result = (instance != null ? instance.battle : null) ?? BattleRun.LastResult ?? new BattleData();//已选会话保存的是本轮结果，静态快照只在无会话时回退
        SaveSystem.Save();//失败也保存等级、经验、技能和已完整拾取的金币，不解锁下一关
        SetTime();
        BattleResultDisplay.SetPercent(process, processHundreds, result.progress);
        BattleResultDisplay.SetMVP(head, heads, result.mvp);
    }
    private void SetTime()
    {
        BattleResultDisplay.SetTime(times, result.costTime);
    }
    public void BackToMap()//返回地图
    {
        if (instance == null)
        {
            SceneManager.LoadScene("MenuScene");//地图依赖已选中的存档，直接运行关卡则先返回选档
            return;
        }
        instance.priScene="DefeatScene";
        SceneManager.LoadScene("MapScene");
    }
    public void TryAgain()//重新挑战
    {
        if (instance != null) instance.priScene = "DefeatScene";
        string sceneName = GetBattleScene();//有有效会话时沿用原关卡，否则重新载入实际测试关卡
        SceneManager.LoadScene(sceneName);
    }

    private string GetBattleScene()//不创建临时存档，直接运行时仅使用当前进程中的关卡记录
    {
        if (instance != null && instance.battle != null && instance.battle.curLevel > 0)
            return "Level" + instance.battle.curLevel.ToString() + "Scene";
        return string.IsNullOrEmpty(PlayerManager.LastBattleScene) ? "Level2Scene" : PlayerManager.LastBattleScene;
    }

    private int GetBattleLevel()//从Level数字Scene中提取失败界面的关卡数字
    {
        if (instance != null && instance.battle != null && instance.battle.curLevel > 0)
            return instance.battle.curLevel;
        string sceneName = GetBattleScene();
        if (sceneName.Length > 10 && sceneName.StartsWith("Level") && sceneName.EndsWith("Scene")
            && int.TryParse(sceneName.Substring(5, sceneName.Length - 10), out int level) && level > 0)
            return level;
        return 2;
    }
}
