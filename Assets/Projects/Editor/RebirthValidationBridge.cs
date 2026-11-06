using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RebirthValidationBridge
{
    private const string Key = "Rebirth.Validation";
    public static void RunBatch()
    {
        //验收只读取交付场景，不运行资产生成器，保留美术后续调整的Sprite和RectTransform。
        EditorSceneManager.OpenScene("Assets/Projects/Scenes/MapScene.unity", OpenSceneMode.Single);
        SessionState.SetBool(Key, true); SessionState.SetBool(Key + ".Batch", Application.isBatchMode);
        EditorApplication.isPlaying = true;
    }
    public static void ValidateEditableAssets()
    {
        Directory.CreateDirectory(RebirthSetup.Audit);
        System.Text.StringBuilder result = new();
        foreach (string path in new[] { "Levels/Level1Scene", "MapScene", "MenuScene", "VictoryScene" })
        {
            var scene = EditorSceneManager.OpenScene("Assets/Projects/Scenes/" + path + ".unity", OpenSceneMode.Single);
            Transform[] nodes = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
            foreach (Transform node in nodes)
            {
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject) > 0) throw new InvalidOperationException(path + ": missing script on " + node.name);
                foreach (Component component in node.GetComponents<Component>())
                {
                    if (component == null) continue;
                    SerializedProperty property = new SerializedObject(component).GetIterator();
                    while (property.Next(true))
                    {
#pragma warning disable CS0618
                        if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue == null && property.objectReferenceEntityIdValue != default)
                            throw new InvalidOperationException(path + ": missing reference " + node.name + "." + property.propertyPath);
#pragma warning restore CS0618
                    }
                }
            }
            if (path == "VictoryScene")
            {
                var button = nodes.First(n => n.name == "RebirthButton");
                if (!(button is RectTransform) || button.GetComponent<UnityEngine.UI.Image>() == null) throw new InvalidOperationException("RebirthButton requires editable RectTransform and Image");
                var control = nodes.Select(n => n.GetComponent<VictoryControl>()).First(c => c != null);
                var fields = new SerializedObject(control);
                if (fields.FindProperty("rebirthButton").objectReferenceValue == null || fields.FindProperty("rebirthMessage").objectReferenceValue == null)
                    throw new InvalidOperationException("Rebirth UI Inspector references are missing");
            }
            if (path == "Levels/Level1Scene")
            {
                var panel = nodes.First(n => n.name == "TutorialHelpPanel");
                if (!(panel is RectTransform) || panel.GetComponent<UnityEngine.UI.Image>() == null) throw new InvalidOperationException("Tutorial help panel requires editable RectTransform and Image");
            }
            result.AppendLine("PASS: " + path + " has no missing script/reference; UI references and RectTransforms are serialized in the scene.");
        }
        File.WriteAllText(Path.Combine(RebirthSetup.Audit, "editable-ui-validation.txt"), result.ToString());
        Debug.Log("REBIRTH_EDITABLE_ASSETS_PASS");
    }
    public static void RunTrainingRegression()
    {
        EditorSceneManager.OpenScene("Assets/Projects/Scenes/MapScene.unity", OpenSceneMode.Single);
        SessionState.SetBool(Key + ".Training", true); SessionState.SetBool(Key + ".TrainingBatch", Application.isBatchMode);
        EditorApplication.isPlaying = true;
    }
    [InitializeOnLoadMethod] private static void Init()
    {
        EditorApplication.playModeStateChanged -= Changed; EditorApplication.playModeStateChanged += Changed;
    }
    private static void Changed(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key + ".Training", false))
        {
            SessionState.SetBool(Key + ".Training", false); new GameObject("TrainingRegression").AddComponent<TrainingRuntimeValidation>();
        }
        if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Key + ".TrainingBatch", false))
        {
            SessionState.SetBool(Key + ".TrainingBatch", false);
            string report = Path.GetFullPath(Path.Combine(Application.dataPath, "../output/TrainingAudit/runtime-validation.txt"));
            EditorApplication.Exit(File.Exists(report) && File.ReadAllText(report).Contains("ALL PASS") ? 0 : 1);
        }
        if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
        {
            SessionState.SetBool(Key, false); new GameObject("RebirthRuntimeValidation").AddComponent<RebirthRuntimeValidation>();
        }
        if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Key + ".Batch", false))
        {
            SessionState.SetBool(Key + ".Batch", false);
            string p = Path.Combine(RebirthSetup.Audit, "runtime-validation.txt");
            EditorApplication.Exit(File.Exists(p) && File.ReadAllText(p).Contains("ALL PASS") ? 0 : 1);
        }
    }
}
