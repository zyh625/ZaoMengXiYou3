using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class ConstellationWeaverValidationBridge
{
    private static string DirectoryPath => Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/ConstellationWeaverAudit")) + Path.DirectorySeparatorChar;
    private const string ScenePath = "Assets/Projects/Editor/ConstellationWeaverValidation.unity";
    static ConstellationWeaverValidationBridge()
    { EditorApplication.update += Poll; EditorApplication.playModeStateChanged += OnPlay; }
    private static void Poll()
    {
        if (Application.isBatchMode || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (File.Exists(DirectoryPath + "build.request"))
        {
            File.Delete(DirectoryPath + "build.request");
            try { ConstellationWeaverSetup.BuildAndValidate(); }
            catch (System.Exception ex) { File.WriteAllText(DirectoryPath + "unity-assets.txt", "FAIL: " + ex); }
        }
        if (File.Exists(DirectoryPath + "run.request"))
        { File.Delete(DirectoryPath + "run.request"); Run(); }
    }
    [MenuItem("造梦西游/ConstellationWeaver/运行隔离验收")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!File.Exists(ScenePath))
        {
            var previous = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            EditorSceneManager.SaveScene(scene, ScenePath);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(scene, true);
        }
        SessionState.SetString("Weaver.PreviousStart", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        SessionState.SetBool("Weaver.Validate", true); EditorApplication.isPlaying = true;
    }
    public static void RunBatch()
    {
        EditorSceneManager.OpenScene("Assets/Projects/Scenes/Levels/Level8Scene.unity", OpenSceneMode.Single);
        ConstellationWeaverSetup.ValidateSprites(); ConstellationWeaverSetup.ValidateReferences();
        Run();
    }
    private static void OnPlay(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("Weaver.Validate", false))
            new GameObject("ConstellationWeaverValidation").AddComponent<ConstellationWeaverRuntimeValidation>();
        if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool("Weaver.Validate", false))
        {
            SessionState.SetBool("Weaver.Validate", false);
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString("Weaver.PreviousStart", ""));
            if (Application.isBatchMode)
                EditorApplication.Exit(File.Exists(DirectoryPath + "runtime-validation.txt") && File.ReadAllText(DirectoryPath + "runtime-validation.txt").Contains("ALL PASS") ? 0 : 1);
        }
    }
}
