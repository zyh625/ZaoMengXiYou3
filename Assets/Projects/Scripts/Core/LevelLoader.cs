using UnityEngine;
using UnityEngine.SceneManagement;

//进入关卡统一走这里：先切到加载场景，由加载界面在后台读取关卡，读完再进入。
//加载场景没有加入构建列表时直接进入关卡，不会卡住。
public static class LevelLoader
{
    public const string LoadingScene = "LoadingScene";
    public static string Target { get; private set; }

    public static void Load(string sceneName)
    {
        Target = sceneName;
        SceneManager.LoadScene(Application.CanStreamedLevelBeLoaded(LoadingScene) ? LoadingScene : sceneName);
    }

    public static int LevelOf(string sceneName)//"Level3Scene" → 3，不是关卡场景返回 0
    {
        if (string.IsNullOrEmpty(sceneName) || !sceneName.StartsWith("Level") || !sceneName.EndsWith("Scene")) return 0;
        return int.TryParse(sceneName.Substring(5, sceneName.Length - 10), out int level) ? level : 0;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay() => Target = null;
}
