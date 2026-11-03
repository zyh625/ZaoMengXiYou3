using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 复制共用模板创建后续关卡；只在编辑器中执行，不参与游戏运行。
public class CreateLevelScene : EditorWindow
{
    private const string TemplatePath = "Assets/Projects/SceneTemplates/LevelTemplate.unity";
    private const string SceneFolder = "Assets/Projects/Scenes";
    [SerializeField] private int levelNumber = 2;//关卡编号同时决定场景名，供地图、存档和下一关使用。

    private string TargetPath => $"{SceneFolder}/Level{levelNumber}Scene.unity";

    [MenuItem("造梦西游/创建关卡场景")]
    private static void OpenWindow()
    {
        CreateLevelScene window = GetWindow<CreateLevelScene>("创建关卡场景");
        window.minSize = new Vector2(460f, 280f);
        window.levelNumber = FindNextAvailableLevel(2);
        window.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("从共用模板创建关卡", EditorStyles.boldLabel);
        levelNumber = EditorGUILayout.IntField("关卡编号（至少 2）", levelNumber);
        EditorGUILayout.LabelField("新场景名称", $"Level{levelNumber}Scene");
        EditorGUILayout.SelectableLabel(TargetPath, EditorStyles.textField, GUILayout.Height(20f));
        EditorGUILayout.Space();

        bool templateExists = AssetDatabase.LoadAssetAtPath<SceneAsset>(TemplatePath) != null;
        bool targetExists = File.Exists(TargetPath) || AssetDatabase.LoadMainAssetAtPath(TargetPath) != null;
        if (!templateExists)
            EditorGUILayout.HelpBox($"找不到模板：{TemplatePath}", MessageType.Error);
        else if (levelNumber < 2)
            EditorGUILayout.HelpBox("此模板用于第二关及后续关卡，请输入至少 2 的编号。", MessageType.Warning);
        else if (targetExists)
            EditorGUILayout.HelpBox("同名场景已存在，不会覆盖。请选择其他编号，或先在 Project 中整理原场景。", MessageType.Warning);
        else
            EditorGUILayout.HelpBox("将复制模板、打开新场景，并将它加入启用的构建场景列表。模板中的第二关环境与敌人配置需要按新关卡调整。", MessageType.Info);

        EditorGUILayout.HelpBox("创建后调整：环境与地面、出生和出口点、镜头每波边界、波次配置、敌人池与 Boss。角色切换、状态特效、金币和战斗 UI 沿用共用预制体。", MessageType.None);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("选择下一个空闲编号"))
            levelNumber = FindNextAvailableLevel(Mathf.Max(2, levelNumber));
        using (new EditorGUI.DisabledScope(!templateExists || levelNumber < 2 || targetExists || EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if (GUILayout.Button("创建并打开新关卡", GUILayout.Height(30f))) CreateScene();
        }
    }

    private void CreateScene()
    {
        // 按钮执行时再次检查，防止窗口打开期间资源发生变化而覆盖现有场景。
        string targetPath = TargetPath;
        if (levelNumber < 2 || File.Exists(targetPath) || AssetDatabase.LoadMainAssetAtPath(targetPath) != null)
        {
            EditorUtility.DisplayDialog("未创建场景", "关卡编号无效，或同名场景已存在。", "确定");
            return;
        }
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TemplatePath) == null)
        {
            EditorUtility.DisplayDialog("未创建场景", $"找不到模板：{TemplatePath}", "确定");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        // “保存场景”对话框可能刚好创建同名场景，因此保存结束后再检查一次。
        if (File.Exists(targetPath) || AssetDatabase.LoadMainAssetAtPath(targetPath) != null)
        {
            EditorUtility.DisplayDialog("未创建场景", "同名场景已存在，已取消创建，原场景保持不变。", "确定");
            return;
        }

        try
        {
            if (!AssetDatabase.IsValidFolder(SceneFolder))
                AssetDatabase.CreateFolder("Assets/Projects", "Scenes");
            if (!AssetDatabase.CopyAsset(TemplatePath, targetPath))
            {
                EditorUtility.DisplayDialog("创建失败", $"无法复制模板到：{targetPath}。请查看 Console。", "确定");
                return;
            }

            EditorSceneManager.OpenScene(targetPath, OpenSceneMode.Single);
            AddToBuildSettings(targetPath);
            SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(targetPath);
            Selection.activeObject = sceneAsset;
            EditorGUIUtility.PingObject(sceneAsset);
            Debug.Log($"已创建 {targetPath} 并加入构建场景列表。接下来请按 Docs/关卡搭建.md 调整关卡内容。", sceneAsset);
            Close();
        }
        catch (Exception exception)
        {
            // 复制成功后若打开或配置失败，保留已创建资源，避免丢失可继续编辑的场景。
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("创建过程未完成", $"请查看 Console，并检查是否已经生成 {targetPath}。已生成的场景会保留。", "确定");
        }
    }

    private static void AddToBuildSettings(string scenePath)
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        for (int i = 0; i < scenes.Count; i++)
        {
            if (scenes[i].path != scenePath) continue;
            scenes[i] = new EditorBuildSettingsScene(scenePath, true);
            EditorBuildSettings.scenes = scenes.ToArray();
            return;
        }
        scenes.Add(new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static int FindNextAvailableLevel(int start)
    {
        int number = Mathf.Max(2, start);
        while (number < int.MaxValue && (File.Exists($"{SceneFolder}/Level{number}Scene.unity") ||
               AssetDatabase.LoadMainAssetAtPath($"{SceneFolder}/Level{number}Scene.unity") != null))
            number++;
        return number;
    }
}
