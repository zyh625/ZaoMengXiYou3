using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class PressureKettleValidationBridge
{
    private const string Key = "PressureKettle.Validate";
    static PressureKettleValidationBridge() { EditorApplication.playModeStateChanged += OnPlay; }
    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch editor for this validation.");
        try
        {
            EditorSceneManager.OpenScene(PressureKettleSetup.ScenePath, OpenSceneMode.Single);
            PressureKettleSetup.BuildAndValidate();
            File.WriteAllText(Path.Combine(PressureKettleSetup.AuditRoot, "runtime-validation.txt"), "RUNNING\n");
            EditorSceneManager.OpenScene(PressureKettleSetup.ScenePath, OpenSceneMode.Single);
            SessionState.SetBool(Key, true); SessionState.SetBool(Key + ".Batch", true);
            EditorApplication.isPlaying = true;
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
    private static void OnPlay(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
        { SessionState.SetBool(Key, false); new GameObject("PressureKettleRuntimeValidation").AddComponent<PressureKettleRuntimeValidation>(); }
        if (change == PlayModeStateChange.EnteredEditMode && Application.isBatchMode && SessionState.GetBool(Key + ".Batch", false))
        {
            SessionState.SetBool(Key + ".Batch", false);
            string path = Path.Combine(PressureKettleSetup.AuditRoot, "runtime-validation.txt");
            EditorApplication.Exit(File.Exists(path) && File.ReadAllText(path).Contains("ALL PASS") ? 0 : 1);
        }
    }
}
