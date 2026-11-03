using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class TrainingValidationBridge
{
    private const string Key="RuntimeTraining.Validation";
    public static void RunBatch()
    {
        TrainingSetup.BuildAndValidate();EditorSceneManager.OpenScene(TrainingSetup.MapPath,OpenSceneMode.Single);
        SessionState.SetBool(Key,true);SessionState.SetBool(Key+".Batch",Application.isBatchMode);EditorApplication.isPlaying=true;
    }
    [InitializeOnLoadMethod]private static void Init(){EditorApplication.playModeStateChanged-=State;EditorApplication.playModeStateChanged+=State;}
    private static void State(PlayModeStateChange state)
    {
        if(state==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key,false))
        {SessionState.SetBool(Key,false);new GameObject("RuntimeTrainingValidation").AddComponent<TrainingRuntimeValidation>();}
        if(state==PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Key+".Batch",false))
        {SessionState.SetBool(Key+".Batch",false);string p=Path.Combine(TrainingSetup.Audit,"runtime-validation.txt");EditorApplication.Exit(File.Exists(p)&&File.ReadAllText(p).Contains("ALL PASS")?0:1);}
    }
}
