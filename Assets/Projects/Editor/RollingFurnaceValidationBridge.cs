using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RollingFurnaceValidationBridge
{
    private const string Key = "RollingFurnace.Validation";
    public static void RunBatch()
    {
        RollingFurnaceSetup.BuildAndValidate();
        EditorSceneManager.OpenScene(RollingFurnaceSetup.ScenePath);
        File.WriteAllText(Path.Combine(RollingFurnaceSetup.Audit, "runtime.txt"), "RUNNING\n");
        SessionState.SetBool(Key, true); EditorApplication.isPlaying = true;
    }
    [InitializeOnLoadMethod]
    private static void Init()
    { EditorApplication.playModeStateChanged -= Changed; EditorApplication.playModeStateChanged += Changed; }
    private static void Changed(PlayModeStateChange change)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (change == PlayModeStateChange.EnteredPlayMode)
            new GameObject("RollingFurnaceRuntimeValidation").AddComponent<RollingFurnaceRuntimeValidation>();
        if (change == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Key, false);
            if (Application.isBatchMode)
            {
                string path = Path.Combine(RollingFurnaceSetup.Audit, "runtime.txt");
                EditorApplication.Exit(File.Exists(path) && File.ReadAllText(path).Contains("ALL PASS") ? 0 : 1);
            }
        }
    }
}
