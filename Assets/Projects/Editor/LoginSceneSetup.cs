using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

//搭建 LoginScene 的登录界面：背景、账号面板、两个输入框、红色错误提示、四个按钮（登录/注册/确认注册/取消）。
//重复执行会删掉上次生成的 LoginCanvas 后重建；场景里的 GameSession、CloudSaveManager 保留，
//原来的 OnGUI 测试登录窗（CloudSaveLoginTester）会被停用。
public static class LoginSceneSetup
{
    private const string ScenePath = "Assets/Projects/Scenes/LoginScene.unity";
    private const string Images = "Assets/Art/UI/Images/";
    private const string Buttons = "Assets/Art/UI/Buttons/Login/";
    private const string CanvasName = "LoginCanvas";
    private static readonly Vector2 Reference = new(1920f, 1080f);
    private const float PanelWidth = 900f;

    //面板图（原图 1536×1024，导入后去透明边约为 1496×1008）上量出的位置，单位为原图像素
    private const float SourceTrimX = 20f, SourceTrimY = 8f;
    private static readonly Rect AccountBox = Rect.MinMaxRect(525f, 297f, 1320f, 398f);
    private static readonly Rect PasswordBox = Rect.MinMaxRect(525f, 460f, 1320f, 565f);
    private const float ErrorY = 618f, ButtonsY = 760f;
    private const float LeftButtonX = 0.3f, RightButtonX = 0.7f, ButtonWidth = 230f;

    [MenuItem("造梦西游/登录/搭建登录界面")]
    public static void Run()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog("登录界面", "请先退出 Play Mode。", "确定");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        try
        {
            Build();
            EditorUtility.DisplayDialog("登录界面", "登录界面已生成并保存到 LoginScene。", "确定");
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            EditorUtility.DisplayDialog("登录界面生成失败", error.Message + "\n\n场景未保存。", "确定");
        }
    }

    private static void Build()
    {
        string[] buttonNames = { "Login", "Register", "Cancel" };
        EnsureSprites(buttonNames.SelectMany(n => new[] { Buttons + n + ".png", Buttons + n + "Light.png" })
            .Append(Images + "LoginBackground.png").Append(Images + "LoginUI.png"));

        TMP_FontAsset chinese = ChineseFontSetup.Ensure();//默认 TMP 字体没有中文，统一换成项目内的中文字体
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == CanvasName) Object.DestroyImmediate(root);
            else foreach (CloudSaveLoginTester tester in root.GetComponentsInChildren<CloudSaveLoginTester>(true)) tester.enabled = false;
        }

        //画布：按 1920×1080 缩放
        GameObject canvasGo = new(CanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        SceneManager.MoveGameObjectToScene(canvasGo, scene);
        canvasGo.layer = LayerMask.NameToLayer("UI");
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = Reference;
        scaler.matchWidthOrHeight = 0.5f;
        Transform canvas = canvasGo.transform;

        //背景：铺满屏幕，保持比例裁切多余部分
        Sprite bgSprite = Load(Images + "LoginBackground.png");
        Image background = Img("Background", canvas, bgSprite, Vector2.zero, Vector2.zero);
        RectTransform bgRect = background.rectTransform;
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        background.raycastTarget = false;
        AspectRatioFitter fitter = background.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = bgSprite.rect.width / bgSprite.rect.height;

        //账号面板
        Sprite panelSprite = Load(Images + "LoginUI.png");
        float panelHeight = PanelWidth * panelSprite.rect.height / panelSprite.rect.width;
        Image panel = Img("LoginPanel", canvas, panelSprite, Vector2.zero, new Vector2(PanelWidth, panelHeight));
        float scale = PanelWidth / panelSprite.rect.width;
        Vector2 ToPanel(float x, float y) => new((x - SourceTrimX) * scale - PanelWidth / 2f, panelHeight / 2f - (y - SourceTrimY) * scale);

        TMP_InputField account = Input("AccountInput", panel.transform, ToPanel(AccountBox.center.x, AccountBox.center.y),
            AccountBox.size * scale, "请输入账号", 32);
        TMP_InputField password = Input("PasswordInput", panel.transform, ToPanel(PasswordBox.center.x, PasswordBox.center.y),
            PasswordBox.size * scale, "请输入密码", 100);
        password.contentType = TMP_InputField.ContentType.Password;
        password.asteriskChar = '*';

        //红色错误提示，位于输入框与按钮之间
        GameObject errorGo = new("ErrorText", typeof(RectTransform), typeof(TextMeshProUGUI));
        errorGo.transform.SetParent(panel.transform, false);
        var errorRect = (RectTransform)errorGo.transform;
        errorRect.anchoredPosition = ToPanel(768f, ErrorY);
        errorRect.sizeDelta = new Vector2(PanelWidth * 0.8f, 44f);
        TextMeshProUGUI error = errorGo.GetComponent<TextMeshProUGUI>();
        error.text = "账号不存在！";
        error.fontSize = 32;
        error.fontStyle = FontStyles.Bold;
        error.alignment = TextAlignmentOptions.Center;
        error.color = new Color(0.86f, 0.12f, 0.1f, 1f);
        error.alpha = 0f;
        error.raycastTarget = false;

        //按钮：登录模式显示 登录/注册，注册模式显示 注册/取消，同一位置两套按钮切换显隐
        Vector2 left = ToPanel(SourceTrimX + panelSprite.rect.width * LeftButtonX, ButtonsY);
        Vector2 right = ToPanel(SourceTrimX + panelSprite.rect.width * RightButtonX, ButtonsY);
        Button login = SpriteButton("LoginButton", panel.transform, "Login", left);
        Button register = SpriteButton("RegisterButton", panel.transform, "Register", right);
        Button confirm = SpriteButton("ConfirmRegisterButton", panel.transform, "Register", left);
        Button cancel = SpriteButton("CancelButton", panel.transform, "Cancel", right);
        confirm.gameObject.SetActive(false);
        cancel.gameObject.SetActive(false);

        foreach (TMP_Text text in canvasGo.GetComponentsInChildren<TMP_Text>(true)) text.font = chinese;
        account.fontAsset = chinese;
        password.fontAsset = chinese;

        LoginPanel controller = canvasGo.AddComponent<LoginPanel>();
        SerializedObject so = new(controller);
        so.FindProperty("chineseFontAsset").objectReferenceValue = chinese;
        so.FindProperty("account").objectReferenceValue = account;
        so.FindProperty("password").objectReferenceValue = password;
        so.FindProperty("loginButton").objectReferenceValue = login;
        so.FindProperty("registerButton").objectReferenceValue = register;
        so.FindProperty("confirmRegisterButton").objectReferenceValue = confirm;
        so.FindProperty("cancelButton").objectReferenceValue = cancel;
        so.FindProperty("errorText").objectReferenceValue = error;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("无法保存 LoginScene。");
        Debug.Log("[LoginSceneSetup] 登录界面已生成：" + CanvasName);
    }

    private static TMP_InputField Input(string name, Transform parent, Vector2 position, Vector2 size, string hint, int limit)
    {
        GameObject go = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources());
        go.name = name;
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0f);//面板图上已画好输入框，这里只保留透明的点击区域
        TMP_InputField field = go.GetComponent<TMP_InputField>();
        field.characterLimit = limit;
        field.lineType = TMP_InputField.LineType.SingleLine;
        field.customCaretColor = true;
        field.caretColor = new Color(1f, 0.92f, 0.7f, 1f);
        field.selectionColor = new Color(1f, 0.85f, 0.4f, 0.35f);
        field.pointSize = 30f;

        var text = (TextMeshProUGUI)field.textComponent;
        text.fontSize = 30f;
        text.color = new Color(1f, 0.95f, 0.82f, 1f);
        text.alignment = TextAlignmentOptions.Left;
        var placeholder = (TextMeshProUGUI)field.placeholder;
        placeholder.text = hint;
        placeholder.fontSize = 28f;
        placeholder.fontStyle = FontStyles.Normal;
        placeholder.color = new Color(0.9f, 0.86f, 0.7f, 0.45f);
        placeholder.alignment = TextAlignmentOptions.Left;
        ((RectTransform)field.textViewport).offsetMin = new Vector2(18f, 6f);
        ((RectTransform)field.textViewport).offsetMax = new Vector2(-18f, -6f);
        return field;
    }

    private static Button SpriteButton(string name, Transform parent, string sprite, Vector2 position)
    {
        Sprite normal = Load(Buttons + sprite + ".png");
        Sprite lighted = Load(Buttons + sprite + "Light.png");
        Image image = Img(name, parent, normal, position, new Vector2(ButtonWidth, ButtonWidth * normal.rect.height / normal.rect.width));
        image.preserveAspect = true;
        Button button = image.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;//发亮由 MouseOn 换图负责
        MouseOn hover = image.gameObject.AddComponent<MouseOn>();
        SerializedObject so = new(hover);
        so.FindProperty("normal").objectReferenceValue = normal;
        so.FindProperty("lighted").objectReferenceValue = lighted;
        so.ApplyModifiedPropertiesWithoutUndo();
        return button;
    }

    private static Image Img(string name, Transform parent, Sprite sprite, Vector2 position, Vector2 size)
    {
        GameObject go = new(name, typeof(RectTransform), typeof(Image));
        go.layer = LayerMask.NameToLayer("UI");
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        return image;
    }

    private static void EnsureSprites(System.Collections.Generic.IEnumerable<string> paths)//新放进来的图若还不是 Sprite，改为单张 Sprite 导入
    {
        AssetDatabase.Refresh();
        foreach (string path in paths)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                throw new InvalidOperationException("找不到图片 " + path);
            if (importer.textureType == TextureImporterType.Sprite) continue;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
    }

    private static Sprite Load(string path)//多图模式时取面积最大的切片（整张图去掉透明边）
    {
        Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
            .OrderByDescending(s => s.rect.width * s.rect.height).FirstOrDefault();
        return sprite != null ? sprite : throw new InvalidOperationException("找不到图片 " + path);
    }
}
