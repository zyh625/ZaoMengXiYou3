using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ClampGuardValidationBridge
{
    private const string Key = "ClampGuard.Validation.Batch";
    public static void RunBatch()
    {
        ClampGuardSetup.BuildAndValidate();
        // Runtime checks use a temporary empty scene, never the disabled Level7 placeholder.
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Key, true);
        File.WriteAllText(Path.Combine(ClampGuardSetup.Audit, "runtime.txt"), "RUNNING\n");
        EditorApplication.isPlaying = true;
    }
    [InitializeOnLoadMethod]
    private static void Init()
    { EditorApplication.playModeStateChanged -= OnPlay; EditorApplication.playModeStateChanged += OnPlay; }
    private static void OnPlay(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
            new GameObject("ClampGuardRuntimeValidation").AddComponent<ClampGuardRuntimeValidation>();
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Key, false);
            string path = Path.Combine(ClampGuardSetup.Audit, "runtime.txt");
            if (Application.isBatchMode) EditorApplication.Exit(File.Exists(path) && File.ReadAllText(path).Contains("ALL PASS") ? 0 : 1);
        }
    }
}
