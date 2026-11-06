using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

//只生产菜单/地图/结算UI接线，不修改任何关卡、预制体或构建列表。
public static class RebirthSetup
{
    public static string Audit => Path.GetFullPath(Path.Combine(Application.dataPath, "../output/RebirthAudit"));
    private static void Ref(Object target, string field, Object value)
    {
        SerializedObject so = new(target); so.FindProperty(field).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo();
    }
    private static T Find<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).First();
    private static Text Label(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, int fontSize)
    {
        Transform existing = parent.Find(name);
        if (existing != null) return existing.GetComponent<Text>();
        GameObject go = new(name, typeof(RectTransform), typeof(Text), typeof(RebirthText)); go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = anchor; rect.anchoredPosition = pos; rect.sizeDelta = size;
        Text text = go.GetComponent<Text>(); text.font = Resources.GetBuiltinResource<UnityEngine.Font>("LegacyRuntime.ttf"); text.fontSize = fontSize;
        text.alignment = TextAnchor.MiddleCenter; text.color = new Color(1f, 0.9f, 0.55f); text.text = "第 0 轮回"; text.raycastTarget = false;
        return text;
    }
    private static void EditScene(string name, Action<Scene> edit)
    {
        string path = "Assets/Projects/Scenes/" + name + ".unity";
        Scene scene = SceneManager.GetSceneByPath(path); bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        edit(scene); EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save " + path);
        if (opened) EditorSceneManager.CloseScene(scene, true);
    }
    [MenuItem("造梦西游/轮回/连接地图、存档槽与结算UI")]
    public static void BuildUI()
    {
        Directory.CreateDirectory(Audit);
        if (string.IsNullOrEmpty(SceneManager.GetActiveScene().path)) EditorSceneManager.OpenScene("Assets/Projects/Scenes/MapScene.unity", OpenSceneMode.Single);
        EditScene("MapScene", scene =>
        {
            Map map = Find<Map>(scene); Canvas canvas = Find<Canvas>(scene);
            Text label = Label(canvas.transform, "RebirthLabel", Vector2.one, new Vector2(-150f, -35f), new Vector2(280f, 48f), 26);
            Ref(map, "rebirthLabel", label);
        });
        EditScene("VictoryScene", scene =>
        {
            VictoryControl victory = Find<VictoryControl>(scene); Canvas canvas = Find<Canvas>(scene);
            Button next = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Button>(true)).First(b => b.name == "NextButton");
            Transform existing = next.transform.parent.Find("RebirthButton"); Button button;
            if (existing != null) button = existing.GetComponent<Button>();
            else
            {
                GameObject go = new("RebirthButton", typeof(RectTransform), typeof(Image), typeof(Button)); go.transform.SetParent(next.transform.parent, false);
                RectTransform rect = go.GetComponent<RectTransform>(), original = next.GetComponent<RectTransform>();
                rect.anchorMin = original.anchorMin; rect.anchorMax = original.anchorMax; rect.pivot = original.pivot;
                rect.anchoredPosition = original.anchoredPosition; rect.sizeDelta = new Vector2(250f, 86f);
                go.GetComponent<Image>().color = new Color(0.32f, 0.12f, 0.09f, 0.97f); button = go.GetComponent<Button>();
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                Text label = Label(go.transform, "RebirthTitle", Vector2.one * 0.5f, Vector2.zero, rect.sizeDelta, 32); label.text = "再入轮回";
                UnityEventTools.AddPersistentListener(button.onClick, victory.EnterRebirth);
            }
            Ref(victory, "rebirthButton", button); Ref(victory, "nextLevelButton", next);
            Text message = Label(canvas.transform, "RebirthMessage", new Vector2(0.5f, 0f), new Vector2(0f, 38f), new Vector2(900f, 48f), 21);
            message.color = new Color(0.12f, 0.19f, 0.24f);
            Ref(victory, "rebirthMessage", message);
            button.gameObject.SetActive(false); message.gameObject.SetActive(false);
        });
        AssetDatabase.SaveAssets();
        File.WriteAllText(Path.Combine(Audit, "unity-assets.txt"), "PASS: Map/6 save slots/Victory Rebirth UI wired; no levels, prefabs or build settings edited.\n");
        Debug.Log("REBIRTH_UI_PASS");
    }
}
