using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class RedFurnaceLordValidationBridge
{
    private const string Key = "RedFurnaceLord.Validation";
    [System.Serializable] private sealed class SceneBackup { public SceneSetup[] scenes; }
    [MenuItem("造梦西游/RedFurnaceLord/运行完整验收")]
    public static void RunBatch()
    {
        if (!Application.isBatchMode)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new System.InvalidOperationException("Save the current scene before running validation.");
            SessionState.SetString(Key + ".Scenes", JsonUtility.ToJson(new SceneBackup { scenes = EditorSceneManager.GetSceneManagerSetup() }));
        }
        RedFurnaceLordSetup.BuildAndValidate();
        EditorSceneManager.OpenScene(RedFurnaceLordSetup.ScenePath);
        File.WriteAllText(Path.Combine(RedFurnaceLordSetup.Audit, "runtime.txt"), "RUNNING\n");
        SessionState.SetBool(Key, true); EditorApplication.isPlaying = true;
    }
    [InitializeOnLoadMethod]
    private static void Init()
    {
        EditorApplication.playModeStateChanged -= Changed; EditorApplication.playModeStateChanged += Changed;
    }
    private static void Changed(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode) new GameObject("RedFurnaceLordValidation").AddComponent<RedFurnaceLordRuntimeValidation>();
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Key, false);
            if (Application.isBatchMode)
            {
                string path = Path.Combine(RedFurnaceLordSetup.Audit, "runtime.txt");
                EditorApplication.Exit(File.Exists(path) && File.ReadAllText(path).Contains("ALL PASS") ? 0 : 1);
            }
            else
            {
                string backup = SessionState.GetString(Key + ".Scenes", "");
                if (!string.IsNullOrEmpty(backup)) EditorApplication.delayCall += () => EditorSceneManager.RestoreSceneManagerSetup(JsonUtility.FromJson<SceneBackup>(backup).scenes);
            }
        }
    }
}
