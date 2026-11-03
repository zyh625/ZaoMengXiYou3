#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

//只改变进入Play Mode后的临时关卡，不改动场景、预制体或正式存档。
public sealed class BattlePracticeWindow : EditorWindow
{
    [SerializeField] private string selectedEnemyGuid;//按资源GUID记住本关选择，重载脚本后仍可继续选择同一种敌人
    private Vector2 scroll;//窗口缩小时允许滚动查看状态与按钮

    [MenuItem("造梦西游/关卡战斗测试", false, 0)]
    public static void OpenWindow()
    {
        BattlePracticeWindow window = GetWindow<BattlePracticeWindow>("关卡战斗测试");
        window.minSize = new Vector2(420f, 330f);
        window.Show();
    }

    private void OnInspectorUpdate() => Repaint();//播放状态变化时及时更新，不向场景写入任何字段

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("当前关卡 · 手动战斗测试", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("场景", SceneManager.GetActiveScene().name);
        EditorGUILayout.HelpBox("选择本关的一种小怪，或直接挑战本关Boss。点击后自动运行，普通波次暂停；测试结束后退出播放即可。测试不会保存闯关进度。", MessageType.Info);

        LevelManager level = BattlePracticeSession.FindCurrentLevel();
        LevelEnemyManager enemies = BattlePracticeSession.FindEnemyManager(level);
        if (enemies == null)
        {
            EditorGUILayout.HelpBox("当前场景没有启用的LevelManager与敌人配置。请先打开要测试的关卡场景。", MessageType.Warning);
        }
        else
        {
            List<EnemyController> prefabs = BattlePracticeSession.GetAvailableEnemies(enemies);
            int selectedIndex = 0;
            for (int i = 0; i < prefabs.Count; i++)
                if (AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefabs[i])) == selectedEnemyGuid) selectedIndex = i;
            string[] labels = new string[prefabs.Count];
            for (int i = 0; i < labels.Length; i++) labels[i] = prefabs[i].name;
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || prefabs.Count == 0))
            {
                if (prefabs.Count > 0)
                {
                    selectedIndex = EditorGUILayout.Popup("选择小怪", selectedIndex, labels);
                    selectedEnemyGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefabs[selectedIndex]));
                }
                else EditorGUILayout.LabelField("选择小怪", "本关未配置小怪");
                if (GUILayout.Button("开始小怪测试（仅生成一只）", GUILayout.Height(32f)))
                    BattlePracticeSession.Start(false, prefabs[selectedIndex]);
            }

            EditorGUILayout.Space(12f);
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ObjectField("本关Boss", enemies.BossPrefab, typeof(Transform), false);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || enemies.BossPrefab == null))
                if (GUILayout.Button("开始 Boss 测试（显示血条与特殊状态槽）", GUILayout.Height(32f)))
                    BattlePracticeSession.Start(true, null);
        }

        EditorGUILayout.Space(12f);
        EditorGUILayout.HelpBox(BattlePracticeSession.Status, MessageType.None);
        using (new EditorGUI.DisabledScope(!BattlePracticeSession.IsPractice || !EditorApplication.isPlaying))
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("重新开始本次测试", GUILayout.Height(28f))) BattlePracticeSession.Restart();
            if (GUILayout.Button("结束测试", GUILayout.Height(28f))) BattlePracticeSession.Stop();
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
    }
}

//SessionState跨播放模式的域重载保存窗口发起的手动测试；不用反射或改动关卡Inspector来跳波次。
[InitializeOnLoad]
public static class BattlePracticeSession
{
    private const string Prefix = "ZaoMeng.BattlePractice.";
    public static string Status => SessionState.GetString(Prefix + "Status", "选择测试目标后点击上方按钮，直接进入游戏。");
    public static bool IsPractice => SessionState.GetInt(Prefix + "Phase", 0) > 0;

    static BattlePracticeSession()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.update -= Update;
        EditorApplication.update += Update;
    }

    public static LevelManager FindCurrentLevel()//忽略布局备份等停用对象，只选择当前关卡中实际执行的管理器
    {
        Scene active = SceneManager.GetActiveScene();
        foreach (LevelManager level in Object.FindObjectsByType<LevelManager>())
            if (level.isActiveAndEnabled && level.gameObject.scene == active) return level;
        return null;
    }

    public static LevelEnemyManager FindEnemyManager(LevelManager level)//编辑态Awake尚未执行时只读查找，不调用ResolveSceneReferences写回场景字段
    {
        if (level == null) return null;
        LevelEnemyManager manager = level.EnemyManager;
        if (manager == null) manager = level.GetComponent<LevelEnemyManager>();
        if (manager == null) manager = level.GetComponentInChildren<LevelEnemyManager>(true);
        if (manager == null)
            foreach (LevelEnemyManager candidate in Object.FindObjectsByType<LevelEnemyManager>())
                if (candidate.isActiveAndEnabled && candidate.gameObject.scene == level.gameObject.scene) { manager = candidate; break; }
        return manager;
    }
    public static List<EnemyController> GetAvailableEnemies(LevelEnemyManager manager)//只列出本关实际配置的种类，重复池不重复显示
    {
        List<EnemyController> result = new();
        if (manager == null) return result;
        foreach (EnemyController prefab in manager.GetEnemyPrefabs())
            if (prefab != null && !result.Contains(prefab)) result.Add(prefab);
        return result;
    }

    public static void Start(bool boss, EnemyController prefab)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        LevelManager level = FindCurrentLevel();
        if (level == null || FindEnemyManager(level) == null || (!boss && prefab == null))
        {
            SetStatus("无法开始：请在当前关卡中配置LevelManager及敌人。", true);
            return;
        }

        SessionState.SetString(Prefix + "Scene", SceneManager.GetActiveScene().path);
        SessionState.SetString(Prefix + "Prefab", prefab != null ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefab)) : "");
        SessionState.SetBool(Prefix + "Boss", boss);
        SessionState.SetBool(Prefix + "Restart", false);
        SessionState.SetInt(Prefix + "Phase", 1);
        SetStatus("正在进入播放模式，等待关卡初始化……");
        EditorApplication.EnterPlaymode();
    }

    public static void Restart()//完整重进Play Mode，各对象池与状态恢复为本轮初始值
    {
        if (!IsPractice || !EditorApplication.isPlaying) return;
        SessionState.SetBool(Prefix + "Restart", true);
        EditorApplication.ExitPlaymode();
    }

    public static void Stop()
    {
        SessionState.SetBool(Prefix + "Restart", false);
        if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (!IsPractice) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            SessionState.SetInt(Prefix + "Phase", 1);
            SessionState.SetFloat(Prefix + "Started", (float)EditorApplication.timeSinceStartup);
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            bool restart = SessionState.GetBool(Prefix + "Restart", false);
            SessionState.SetInt(Prefix + "Phase", 0);
            SessionState.SetBool(Prefix + "Restart", false);
            SetStatus("测试已结束，场景与预制体保持原来的配置。");
            if (restart)
            {
                EditorApplication.delayCall += () =>
                {
                    bool boss = SessionState.GetBool(Prefix + "Boss", false);
                    string path = AssetDatabase.GUIDToAssetPath(SessionState.GetString(Prefix + "Prefab", ""));
                    Start(boss, LoadEnemyPrefab(path));
                };
            }
        }
    }

    private static void Update()
    {
        if (SessionState.GetInt(Prefix + "Phase", 0) != 1 || !EditorApplication.isPlaying || EditorApplication.isPaused) return;
        if (SceneManager.GetActiveScene().path != SessionState.GetString(Prefix + "Scene", ""))
        {
            Fail("关卡场景已改变，本次测试已取消。");
            return;
        }
        LevelManager level = FindCurrentLevel();
        if (level != null && level.IsInitialized)
        {
            bool boss = SessionState.GetBool(Prefix + "Boss", false);
            string path = AssetDatabase.GUIDToAssetPath(SessionState.GetString(Prefix + "Prefab", ""));
            EnemyController prefab = LoadEnemyPrefab(path);
            if (!level.BeginBattlePractice(prefab, boss))
            {
                Fail("测试目标未能生成，请检查关卡敌人配置与Console。");
                return;
            }
            SessionState.SetInt(Prefix + "Phase", 2);
            SetStatus(boss ? "Boss测试进行中：普通波次已暂停，血条和本关特殊状态槽已连接。" : $"小怪测试进行中：仅生成一只 {prefab.name}，不会进入下一波。");
            EditorApplication.ExecuteMenuItem("Window/General/Game");//把键盘焦点交给游戏，点击测试后即可操作角色
        }
        else if (level == null || !level.enabled)
            Fail("关卡初始化失败，请检查Console中的场景配置错误。");
        else if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Prefix + "Started", 0f) > 20d)
            Fail("等待关卡初始化超时，请检查Console中的场景配置错误。");
    }

    private static EnemyController LoadEnemyPrefab(string path)//预制体主资源是GameObject，再取根控制器，避免把Component当主资源加载
    {
        GameObject prefab = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
        return prefab != null ? prefab.GetComponent<EnemyController>() : null;
    }
    private static void Fail(string message)
    {
        SessionState.SetBool(Prefix + "Restart", false);
        SetStatus(message, true);
        SessionState.SetInt(Prefix + "Phase", 0);
        EditorApplication.ExitPlaymode();
    }

    private static void SetStatus(string message, bool error = false)
    {
        SessionState.SetString(Prefix + "Status", message);
        if (error) Debug.LogError("[关卡战斗测试] " + message);
    }

}
#endif
