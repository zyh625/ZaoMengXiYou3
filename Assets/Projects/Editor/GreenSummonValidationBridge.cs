using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GreenSummonValidationBridge
{
    private const string Key="GreenSummon.Validation";
    public static void RunBatch()
    {
        if(!Application.isBatchMode)throw new System.InvalidOperationException("Batch validation only.");
        GreenSummonSetup.BuildAndValidate();SessionState.SetBool(Key+".Batch",true);Run();
    }
    [MenuItem("造梦西游/GreenSummon/运行技能与战旗来源验收")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        if(SceneManager.GetActiveScene().path!=GreenSummonSetup.ScenePath)throw new System.InvalidOperationException("请先打开Level6Scene。");
        Directory.CreateDirectory(GreenSummonSetup.Audit);File.WriteAllText(Path.Combine(GreenSummonSetup.Audit,"runtime-validation.txt"),"RUNNING\n");
        SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;
    }
    [InitializeOnLoadMethod]private static void Init(){EditorApplication.playModeStateChanged-=OnPlay;EditorApplication.playModeStateChanged+=OnPlay;}
    private static void OnPlay(PlayModeStateChange state)
    {
        if(state==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key,false))
        {SessionState.SetBool(Key,false);new GameObject("GreenSummonRuntimeValidation").AddComponent<GreenSummonRuntimeValidation>();}
        if(state==PlayModeStateChange.EnteredEditMode && Application.isBatchMode && SessionState.GetBool(Key+".Batch",false))
        {
            SessionState.SetBool(Key+".Batch",false);string path=Path.Combine(GreenSummonSetup.Audit,"runtime-validation.txt");
            EditorApplication.Exit(File.Exists(path)&&File.ReadAllText(path).Contains("ALL PASS")?0:1);
        }
    }
}
