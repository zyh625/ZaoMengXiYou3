using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class StarShearGuardValidationBridge
{
    private const string Key = "StarShearGuard.Validation.Batch";
    private const string TestScene = "Assets/Projects/Editor/StarShearGuardValidation.unity";
    private static string Request => Path.GetFullPath("output/StarShearGuardAudit/run.request");
    public static void RunBatch()
    {
        StarShearGuardSetup.BuildAndValidate();
        Run();
    }
    [MenuItem("造梦西游/StarShearGuard/运行隔离验收")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!File.Exists(TestScene))
        {
            var previous = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var test = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            EditorSceneManager.SaveScene(test, TestScene);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(test, true);
        }
        SessionState.SetString(Key + ".PreviousStart", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(TestScene);
        SessionState.SetBool(Key, true);
        File.WriteAllText(Path.Combine(StarShearGuardSetup.Audit, "runtime.txt"), "RUNNING\n");
        EditorApplication.isPlaying = true;
    }
    [InitializeOnLoadMethod]
    private static void Init()
    {
        EditorApplication.playModeStateChanged -= OnPlay; EditorApplication.playModeStateChanged += OnPlay;
        EditorApplication.update -= Poll; EditorApplication.update += Poll;
    }
    private static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Request)) return;
        File.Delete(Request);
        try { RunBatch(); }
        catch (System.Exception ex)
        { Directory.CreateDirectory(StarShearGuardSetup.Audit); File.WriteAllText(Path.Combine(StarShearGuardSetup.Audit, "runtime.txt"), "FAIL: " + ex); Debug.LogException(ex); }
    }
    private static void OnPlay(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
            new GameObject("StarShearGuardRuntimeValidation").AddComponent<StarShearGuardRuntimeValidation>();
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Key, false);
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(Key + ".PreviousStart", ""));
            string path = Path.Combine(StarShearGuardSetup.Audit, "runtime.txt");
            if (Application.isBatchMode) EditorApplication.Exit(File.Exists(path) && File.ReadAllText(path).Contains("ALL PASS") ? 0 : 1);
        }
    }
}
