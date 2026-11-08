using UnityEngine;

//打包后的窗口：默认 800x600 窗口模式，可以拖动边框调整大小，但始终保持 4:3。
//Unity 不能在拖动过程中锁住比例，所以等玩家松手（尺寸停止变化一小会儿）后，按改动较大的那一边把窗口修正回 4:3。
//画面内容（镜头和 UI）本来就按窗口大小缩放，窗口等比放大后内部跟着等比放大。全屏时不处理。编辑器里不生效。
public sealed class WindowAspect : MonoBehaviour
{
    private const float Ratio = 4f / 3f;
    private const int MinWidth = 800, MinHeight = 600;
    private const string SetupKey = "WindowAspect.Setup.v1";//第一次运行新包时强制回到 800x600 窗口（覆盖以前全屏运行记住的设置）
    private const float SettleTime = 0.25f;

    private int lastWidth, lastHeight, keptWidth, keptHeight;
    private float changedAt;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (Application.isEditor || !Application.isPlaying) return;
        var go = new GameObject("WindowAspect") { hideFlags = HideFlags.HideAndDontSave };
        DontDestroyOnLoad(go);
        go.AddComponent<WindowAspect>();
    }

    private void Start()
    {
        if (PlayerPrefs.GetInt(SetupKey, 0) == 0)
        {
            Screen.SetResolution(MinWidth, MinHeight, FullScreenMode.Windowed);
            PlayerPrefs.SetInt(SetupKey, 1);
            PlayerPrefs.Save();
            keptWidth = lastWidth = MinWidth;
            keptHeight = lastHeight = MinHeight;
        }
        else
        {
            keptWidth = lastWidth = Screen.width;
            keptHeight = lastHeight = Screen.height;
        }
        changedAt = Time.unscaledTime;
    }

    private void Update()
    {
        if (Screen.fullScreenMode != FullScreenMode.Windowed) return;
        int w = Screen.width, h = Screen.height;
        if (w <= 0 || h <= 0) return;
        if (w != lastWidth || h != lastHeight)//还在拖动：记下时间，等停下来再修正
        {
            lastWidth = w; lastHeight = h;
            changedAt = Time.unscaledTime;
            return;
        }
        if (Time.unscaledTime - changedAt < SettleTime) return;
        if (Mathf.Abs((float)w / h - Ratio) < 0.01f && w >= MinWidth) { keptWidth = w; keptHeight = h; return; }

        bool byWidth = Mathf.Abs(w - keptWidth) >= Mathf.Abs(h - keptHeight);//玩家主要拖的是哪一边，就以哪一边为准
        int nw = byWidth ? w : Mathf.RoundToInt(h * Ratio);
        //不超过屏幕可用范围（留出标题栏和任务栏），也不小于 800x600
        Resolution display = Screen.currentResolution;
        int maxH = Mathf.Max(MinHeight, Mathf.RoundToInt(display.height * 0.9f));
        int maxW = Mathf.Max(MinWidth, display.width);
        nw = Mathf.Clamp(nw, MinWidth, Mathf.Min(maxW, Mathf.RoundToInt(maxH * Ratio)));
        int nh = Mathf.RoundToInt(nw / Ratio);
        Screen.SetResolution(nw, nh, FullScreenMode.Windowed);
        keptWidth = lastWidth = nw;
        keptHeight = lastHeight = nh;
        changedAt = Time.unscaledTime;
    }
}
