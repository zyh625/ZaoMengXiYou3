using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class WestKingValidationBridge
{
    private const string Key="WestKing.Validation";
    public static void RunBatch()
    {
        if(!Application.isBatchMode)throw new System.InvalidOperationException("Batch validation only.");
        WestKingSetup.BuildAndValidate();SessionState.SetBool(Key+".Batch",true);Run();
    }
    [MenuItem("造梦西游/WestKing/运行护珠与物理验收")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!=WestKingSetup.ScenePath)throw new System.InvalidOperationException("请先打开 Level5Scene，再运行广目天王验收。");
        SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;
    }
    [InitializeOnLoadMethod]
    private static void Init(){EditorApplication.playModeStateChanged-=OnPlay;EditorApplication.playModeStateChanged+=OnPlay;}
    private static void OnPlay(PlayModeStateChange state)
    {
        if(state==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key,false))
        {SessionState.SetBool(Key,false);new GameObject("WestKingRuntimeValidation").AddComponent<WestKingRuntimeValidation>();}
        if(state==PlayModeStateChange.EnteredEditMode && Application.isBatchMode && SessionState.GetBool(Key+".Batch",false))
        {
            SessionState.SetBool(Key+".Batch",false);string report=Path.Combine(WestKingSetup.Audit,"runtime-validation.txt");
            EditorApplication.Exit(File.Exists(report)&&File.ReadAllText(report).Contains("ALL PASS")?0:1);
        }
    }
}
