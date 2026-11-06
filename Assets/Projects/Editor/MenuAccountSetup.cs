using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

//主菜单的“切换账号 / 注销账号”：给两个按钮接上点击事件，并搭建注销确认界面
//（EnsureWithdraw 底图 + 密码输入框 + 红色报错 + 确定/取消）。确定/取消、遮罩直接复制 SelectDelete 里的同名物体，外观保持一致。
//重复执行会删除上次生成的 WithdrawConfirm 后重建。
public static class MenuAccountSetup
{
    private const string ScenePath = "Assets/Projects/Scenes/MenuScene.unity";
    private const string PanelImage = "Assets/Art/UI/Images/EnsureWithdraw.png";
    private const string PanelName = "WithdrawConfirm";
    private const float PanelWidth = 560f;
    private static readonly Vector2 PanelCenter = new(0f, -20f);

    //在去掉透明边后的底图上量出的比例（0～1，原点在左上）
    private static readonly Rect InputBox = Rect.MinMaxRect(0.393f, 0.486f, 0.770f, 0.579f);
    private const float ErrorY = 0.703f, ButtonsY = 0.86f, ButtonsX = 85f;

    [MenuItem("造梦西游/主菜单/搭建切换与注销账号")]
    public static void Run()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog("账号", "请先退出 Play Mode。", "确定");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        try
        {
            Build();
            EditorUtility.DisplayDialog("账号", "切换账号与注销账号已接好，并保存到 MenuScene。", "确定");
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            EditorUtility.DisplayDialog("账号界面生成失败", error.Message + "\n\n场景未保存。", "确定");
        }
    }

    private static void Build()
    {
        TMP_FontAsset chinese = ChineseFontSetup.Ensure();
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        T Find<T>() where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).FirstOrDefault();
        Transform Child(string path) => scene.GetRootGameObjects().Select(r => r.transform.Find(path)).FirstOrDefault(t => t != null)
            ?? throw new InvalidOperationException("MenuScene 中找不到 " + path);

        MenuButton menu = Find<MenuButton>() ?? throw new InvalidOperationException("MenuScene 中找不到 MenuButton。");
        Transform canvas = Child("MainButton").parent;
        Transform selectDelete = Child("SelectDelete");

        //1. 主菜单两个按钮
        Wire(Child("MainButton/Switch").GetComponent<Button>(), menu.SwitchAccount);
        Wire(Child("MainButton/Withdraw").GetComponent<Button>(), menu.ShowWithdraw);

        //2. 注销确认界面
        Transform old = canvas.Find(PanelName);
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var panel = new GameObject(PanelName, typeof(RectTransform));
        panel.layer = LayerMask.NameToLayer("UI");
        panel.transform.SetParent(canvas, false);
        panel.transform.SetAsLastSibling();

        Object.Instantiate(selectDelete.Find("Block").gameObject, panel.transform, false).name = "Block";//遮住主菜单，挡住点击

        Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(PanelImage).OfType<Sprite>()
            .OrderByDescending(s => s.rect.width * s.rect.height).FirstOrDefault()
            ?? throw new InvalidOperationException("找不到图片 " + PanelImage);
        float height = PanelWidth * sprite.rect.height / sprite.rect.width;
        GameObject ui = new("ui", typeof(RectTransform), typeof(Image));
        ui.layer = panel.layer;
        ui.transform.SetParent(panel.transform, false);
        var uiRect = (RectTransform)ui.transform;
        uiRect.anchoredPosition = PanelCenter;
        uiRect.sizeDelta = new Vector2(PanelWidth, height);
        ui.GetComponent<Image>().sprite = sprite;
        Vector2 At(float fx, float fy) => PanelCenter + new Vector2((fx - 0.5f) * PanelWidth, (0.5f - fy) * height);

        TMP_InputField password = PasswordField(panel.transform, At(InputBox.center.x, InputBox.center.y),
            new Vector2(InputBox.width * PanelWidth, InputBox.height * height), chinese);

        GameObject errorGo = new("ErrorText", typeof(RectTransform), typeof(TextMeshProUGUI));
        errorGo.layer = panel.layer;
        errorGo.transform.SetParent(panel.transform, false);
        var errorRect = (RectTransform)errorGo.transform;
        errorRect.anchoredPosition = At(0.5f, ErrorY);
        errorRect.sizeDelta = new Vector2(PanelWidth * 0.7f, 26f);
        TextMeshProUGUI error = errorGo.GetComponent<TextMeshProUGUI>();
        error.font = chinese;
        error.text = "密码错误！";
        error.fontSize = 20;
        error.fontStyle = FontStyles.Bold;
        error.alignment = TextAlignmentOptions.Center;
        error.color = new Color(0.86f, 0.12f, 0.1f, 1f);
        error.alpha = 0f;
        error.raycastTarget = false;

        float buttonsY = At(0.5f, ButtonsY).y;
        Button confirm = CopyButton(selectDelete.Find("yes"), panel.transform, "yes", new Vector2(-ButtonsX, buttonsY), menu.ConfirmWithdraw);
        CopyButton(selectDelete.Find("back"), panel.transform, "back", new Vector2(ButtonsX, buttonsY), menu.CancelWithdraw);
        panel.SetActive(false);

        //3. 引用接到 MenuButton
        SerializedObject so = new(menu);
        so.FindProperty("withdrawUI").objectReferenceValue = panel;
        so.FindProperty("withdrawPassword").objectReferenceValue = password;
        so.FindProperty("withdrawError").objectReferenceValue = error;
        so.FindProperty("withdrawConfirmButton").objectReferenceValue = confirm;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("无法保存 MenuScene。");
        Debug.Log("[MenuAccountSetup] 已接好切换账号/注销账号，并生成 " + PanelName);
    }

    private static TMP_InputField PasswordField(Transform parent, Vector2 position, Vector2 size, TMP_FontAsset font)
    {
        GameObject go = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources());
        go.name = "PasswordInput";
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0f);//底图上已画好输入框

        TMP_InputField field = go.GetComponent<TMP_InputField>();
        field.contentType = TMP_InputField.ContentType.Password;
        field.asteriskChar = '*';
        field.lineType = TMP_InputField.LineType.SingleLine;
        field.characterLimit = 100;
        field.fontAsset = font;
        field.pointSize = 16f;
        field.customCaretColor = true;
        field.caretColor = new Color(0.15f, 0.12f, 0.1f, 1f);

        var text = (TextMeshProUGUI)field.textComponent;
        text.font = font;
        text.fontSize = 16f;
        text.color = new Color(0.15f, 0.12f, 0.1f, 1f);
        text.alignment = TextAlignmentOptions.Left;
        var placeholder = (TextMeshProUGUI)field.placeholder;
        placeholder.font = font;
        placeholder.text = "请输入当前账号的密码";
        placeholder.fontSize = 14f;
        placeholder.fontStyle = FontStyles.Normal;
        placeholder.color = new Color(0.3f, 0.27f, 0.22f, 0.5f);
        placeholder.alignment = TextAlignmentOptions.Left;
        ((RectTransform)field.textViewport).offsetMin = new Vector2(8f, 2f);
        ((RectTransform)field.textViewport).offsetMax = new Vector2(-8f, -2f);
        return field;
    }

    private static Button CopyButton(Transform source, Transform parent, string name, Vector2 position, UnityEngine.Events.UnityAction action)
    {
        if (source == null) throw new InvalidOperationException("SelectDelete 中找不到可复用的按钮。");
        GameObject copy = Object.Instantiate(source.gameObject, parent, false);
        copy.name = name;
        copy.SetActive(true);
        ((RectTransform)copy.transform).anchoredPosition = position;
        Button button = copy.GetComponent<Button>();
        Wire(button, action);
        return button;
    }

    private static void Wire(Button button, UnityEngine.Events.UnityAction action)//清掉原有的持久事件，只保留这一个
    {
        if (button == null) throw new InvalidOperationException("找不到按钮组件。");
        for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            UnityEventTools.RemovePersistentListener(button.onClick, i);
        UnityEventTools.AddPersistentListener(button.onClick, action);
        EditorUtility.SetDirty(button);
    }
}
