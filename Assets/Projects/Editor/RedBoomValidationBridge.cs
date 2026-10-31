using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RedBoomValidationBridge
{
    private const string Key="RedBoom.RunValidation";
    [MenuItem("造梦西游/RedBoom/运行物理与效果验收")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;
    }
    public static void RunBatch()
    {
        if(!Application.isBatchMode)throw new System.InvalidOperationException("RunBatch is for an isolated batch editor.");
        Directory.CreateDirectory(RedBoomValidationPaths.AuditDirectory);
        File.WriteAllText(RedBoomValidationPaths.FilePath("runtime-validation.txt"),"RUNNING\n");
        EditorSceneManager.OpenScene("Assets/Projects/Scenes/Levels/Level5Scene.unity",OpenSceneMode.Single);
        SessionState.SetBool(Key+".Batch",true);
        Run();
    }
    [InitializeOnLoadMethod]
    private static void Watch()
    {
        EditorApplication.playModeStateChanged-=OnPlay;EditorApplication.playModeStateChanged+=OnPlay;
        if(!Application.isBatchMode && (File.Exists(RedBoomValidationPaths.FilePath("build.request"))||File.Exists(RedBoomValidationPaths.FilePath("run.request"))))EditorApplication.delayCall+=ProcessRequest;
    }
    private static void ProcessRequest()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling)return;
        string build=RedBoomValidationPaths.FilePath("build.request"),run=RedBoomValidationPaths.FilePath("run.request");
        if(File.Exists(build)){File.Delete(build);RedBoomSetup.BuildAndValidate();}
        if(File.Exists(run)){File.Delete(run);Run();}
    }
    private static void OnPlay(PlayModeStateChange change)
    {
        if(change==PlayModeStateChange.EnteredEditMode && Application.isBatchMode && SessionState.GetBool(Key+".Batch",false))
        {
            SessionState.SetBool(Key+".Batch",false);
            string path=RedBoomValidationPaths.FilePath("runtime-validation.txt");
            EditorApplication.Exit(File.Exists(path)&&File.ReadAllText(path).Contains("ALL PASS")?0:1);
            return;
        }
        if(change!=PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key,false))return;
        SessionState.SetBool(Key,false);new GameObject("RedBoomRuntimeValidation").AddComponent<RedBoomRuntimeValidation>();
    }
}
