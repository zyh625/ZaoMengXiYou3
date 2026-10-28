using System;
using UnityEngine;

//菜单暂停共用入口：冻结游戏时间，同时通知角色输入暂停；UI按钮本身继续响应。
public static class GamePause
{
    public static bool IsPaused { get; private set; }//不把其他用途的时间倍率误当作菜单暂停
    public static event Action<bool> Changed;//输入系统在恢复时同步暂停期间已经松开的按键
    private static float previousTimeScale = 1f;//关闭菜单后恢复进入菜单前的游戏速度

    public static void Pause()
    {
        if (IsPaused) return;
        previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        IsPaused = true;
        Time.timeScale = 0f;
        Changed?.Invoke(true);
    }

    public static void Resume()
    {
        if (!IsPaused) return;
        IsPaused = false;
        Time.timeScale = previousTimeScale;
        Changed?.Invoke(false);
    }

    public static void Reset()//离开当前关卡后，新地图、菜单和结算场景使用正常时间倍率
    {
        bool wasPaused = IsPaused;
        IsPaused = false;
        previousTimeScale = 1f;
        Time.timeScale = 1f;
        if (wasPaused) Changed?.Invoke(false);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Initialize()//关闭Domain Reload时，也不沿用上一次运行留下的暂停标记和订阅
    {
        Changed = null;
        Reset();
    }
}
