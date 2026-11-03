using System.IO;
using UnityEditor;
using UnityEngine;

public static class RedChainPlayValidation
{
    private const string RequestKey = "RedChain.RunValidation";
    [MenuItem("造梦西游/RedChain/运行物理与行为验收")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        SessionState.SetBool(RequestKey,true);
        EditorApplication.isPlaying = true;
    }
    [InitializeOnLoadMethod]
    private static void Watch()
    {
        EditorApplication.playModeStateChanged -= OnPlay;
        EditorApplication.playModeStateChanged += OnPlay;
        //显式的一次性本地验收请求；没有该文件时不自动运行测试。
        const string request = "Temp/RedChainAudit/run-validation.request";
        if (File.Exists(request))
        {
            File.Delete(request);
            EditorApplication.delayCall += Run;
        }
    }
    private static void OnPlay(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(RequestKey,false)) return;
        SessionState.SetBool(RequestKey,false);
        new GameObject("RedChainRuntimeValidation").AddComponent<RedChainRuntimeValidation>();
    }
}
