using System;
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class ResonantStarOwlValidationBridge
{
    static ResonantStarOwlValidationBridge() { EditorApplication.update += ProcessRequest; }
    public static void RunBatch()
    {
        if(!Application.isBatchMode) throw new InvalidOperationException("Use isolated batch editor");
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ResonantStarOwlSetup.ScenePath);
        ResonantStarOwlSetup.BuildAndValidate();
        UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene);
        SessionState.SetBool("Owl.Run",true);
        EditorApplication.playModeStateChanged += OnPlay;
        EditorApplication.isPlaying=true;
    }
    [InitializeOnLoadMethod]
    private static void Watch() { EditorApplication.playModeStateChanged -= OnPlay; EditorApplication.playModeStateChanged += OnPlay; }
    private static void OnPlay(PlayModeStateChange state)
    {
        if(state==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("Owl.Run",false))
        { SessionState.SetBool("Owl.Run",false); new GameObject("OwlRuntimeValidation").AddComponent<ResonantStarOwlRuntimeValidation>(); }
    }
    private static void ProcessRequest()
    {
        string path = Path.GetFullPath("Logs/ResonantStarOwlAudit/build.request");
        if (!File.Exists(path) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        File.Move(path,path+"."+DateTime.UtcNow.Ticks+".handled");
        try { ResonantStarOwlSetup.BuildAndValidate(); }
        catch(Exception e) { Debug.LogException(e); File.WriteAllText(Path.Combine(ResonantStarOwlSetup.AuditRoot,"failure.txt"),e.ToString()); }
    }
}
