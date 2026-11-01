using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GreenHammerValidationBridge
{
    private const string Key="GreenHammer.Validation";
    public static void RunBatch()
    {
        if(!Application.isBatchMode)throw new System.InvalidOperationException("Batch validation only.");
        GreenHammerSetup.BuildAndValidate();SessionState.SetBool(Key+".Batch",true);Run();
    }
    [MenuItem("造梦西游/GreenHammer/运行三招与重力验收")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        if(SceneManager.GetActiveScene().path!=GreenHammerSetup.ScenePath)throw new System.InvalidOperationException("请先打开Level6Scene。");
        Directory.CreateDirectory(GreenHammerSetup.Audit);File.WriteAllText(Path.Combine(GreenHammerSetup.Audit,"runtime-validation.txt"),"RUNNING\n");
        SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;
    }
    [InitializeOnLoadMethod]
    private static void Init(){EditorApplication.playModeStateChanged-=OnPlay;EditorApplication.playModeStateChanged+=OnPlay;}
    private static void OnPlay(PlayModeStateChange state)
    {
        if(state==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key,false))
        {SessionState.SetBool(Key,false);new GameObject("GreenHammerRuntimeValidation").AddComponent<GreenHammerRuntimeValidation>();}
        if(state==PlayModeStateChange.EnteredEditMode && Application.isBatchMode && SessionState.GetBool(Key+".Batch",false))
        {
            SessionState.SetBool(Key+".Batch",false);string path=Path.Combine(GreenHammerSetup.Audit,"runtime-validation.txt");
            EditorApplication.Exit(File.Exists(path)&&File.ReadAllText(path).Contains("ALL PASS")?0:1);
        }
    }
}
