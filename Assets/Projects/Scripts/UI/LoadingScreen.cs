using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

//加载界面：在后台读取目标关卡，进度条跟着真实进度走，读完再切过去。
//Unity 在“读完但先别切换”时进度停在 0.9，所以 0.9 按 100% 显示；进度条满的那一刻正好可以进入。
//显示的进度平滑追赶真实进度，并且至少显示 minDuration 秒，避免加载太快时画面一闪而过。
public sealed class LoadingScreen : MonoBehaviour
{
    [SerializeField] private Image fill;//进度条金色光带，Image 类型为 Filled（水平、从左到右）
    [SerializeField] private RectTransform cloud;//跟着进度前端走的筋斗云
    [SerializeField] private RectTransform track;//祥云移动的范围：光带的左右两端
    [SerializeField] private SpriteNumber percent;
    [SerializeField] private TextMeshProUGUI levelTitle, tip;
    [SerializeField, Min(0f)] private float minDuration = 1.5f;
    [SerializeField, Min(0.1f)] private float fillSpeed = 1.2f;//每秒最多前进的进度
    [SerializeField] private string fallbackScene = "Level1Scene";//直接运行加载场景测试时进入的关卡
    [Tooltip("第 1～9 关的名称，显示在关卡名牌匾上")]
    [SerializeField] private string[] levelNames =
        { "云栈鸟道", "南天门", "东天琵琶台", "南天剑阁", "西天赤龙渊", "北天宝伞峰", "赤炉炼狱", "九曜星宫", "无相天宫" };
    [SerializeField, TextArea(1, 2)] private string[] tips =
    {
        "按 1～4 切换出场角色，切换后有一小段冷却。",
        "暴击率、减伤等属性可在背包左侧查看。",
        "Boss 必定掉落金币，并有机会掉落装备和法宝。",
        "掉在地上的装备不会自动拾取，记得走过去捡起来。",
        "背包右下角的“出售白装”可一键卖掉全部普通装备。",
        "法宝没有品质之分，在法宝界面花金币升级。",
        "装备法宝后按 H 使用，冷却期间法宝会消失，也不能更换。",
        "被动技能永久生效，在技能界面切换到“被动”升级。",
        "按键配置里可以给同一个技能绑定多个按键。",
        "每次轮回敌人都会变强，但也会掉落更高品质的装备。",
        "通关第 9 关后可再入轮回，保留全部能力从头再闯。",
    };

    private static readonly string[] Numbers = { "零", "一", "二", "三", "四", "五", "六", "七", "八", "九" };

    private void Awake()
    {
        if (UIFonts.Chinese != null)
        {
            if (levelTitle != null) levelTitle.font = UIFonts.Chinese;
            if (tip != null) tip.font = UIFonts.Chinese;
        }
    }

    private void Start()
    {
        string target = string.IsNullOrEmpty(LevelLoader.Target) ? fallbackScene : LevelLoader.Target;
        int level = LevelLoader.LevelOf(target);
        if (levelTitle != null)
        {
            string name = level >= 1 && level <= levelNames.Length ? levelNames[level - 1] : "";
            string number = level >= 1 && level <= 9 ? $"第{Numbers[level]}关" : "";
            levelTitle.SetText(string.IsNullOrEmpty(name) ? number : $"{number} · {name}");
        }
        if (tip != null && tips.Length > 0) tip.SetText("小贴士：" + tips[Random.Range(0, tips.Length)]);
        Show(0f);
        StartCoroutine(Load(target));
    }

    private IEnumerator Load(string target)
    {
        yield return null;//先把加载界面画出来，再开始读关卡
        float started = Time.unscaledTime, shown = 0f;
        if (!Application.CanStreamedLevelBeLoaded(target))
        {
            Debug.LogError($"[加载界面] 找不到场景 {target}，请检查构建列表");
            yield break;
        }
        AsyncOperation op = SceneManager.LoadSceneAsync(target);
        op.allowSceneActivation = false;
        while (true)
        {
            float real = Mathf.Clamp01(op.progress / 0.9f);
            float byTime = minDuration > 0f ? Mathf.Clamp01((Time.unscaledTime - started) / minDuration) : 1f;
            shown = Mathf.MoveTowards(shown, Mathf.Min(real, byTime), fillSpeed * Time.unscaledDeltaTime);
            Show(shown);
            if (shown >= 1f && op.progress >= 0.9f) break;
            yield return null;
        }
        yield return new WaitForSecondsRealtime(0.15f);//让玩家看到 100%
        op.allowSceneActivation = true;
    }

    private void Show(float value)
    {
        if (fill != null) fill.fillAmount = value;
        percent?.SetText(Mathf.RoundToInt(value * 100f) + "%");
        if (cloud != null && track != null)
        {
            float half = track.rect.width * 0.5f;
            Vector2 p = cloud.anchoredPosition;
            p.x = track.anchoredPosition.x - half + track.rect.width * value;
            cloud.anchoredPosition = p;
        }
    }
}
