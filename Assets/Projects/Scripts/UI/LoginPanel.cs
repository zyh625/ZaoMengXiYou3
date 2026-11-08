using System.Collections;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using AccountResult = CloudSaveClient.AccountResult;

//登录界面：同一个面板切换“登录 / 注册”两种模式。
//登录模式：左下“登录”、右下“注册”；注册模式：左下“注册”、右下“取消”。切换时清空账号和密码。
//错误提示出现时瞬间显示，停留一会儿后逐渐淡出。
public sealed class LoginPanel : MonoBehaviour
{
    [Header("输入")]
    [SerializeField] private TMP_InputField account;
    [SerializeField] private TMP_InputField password;
    [Header("按钮")]
    [SerializeField] private Button loginButton;//登录模式左下
    [SerializeField] private Button registerButton;//登录模式右下，进入注册模式
    [SerializeField] private Button confirmRegisterButton;//注册模式左下
    [SerializeField] private Button cancelButton;//注册模式右下，回到登录模式
    [Header("错误提示")]
    [SerializeField] private TextMeshProUGUI errorText;
    [SerializeField, Min(0f)] private float errorHold = 1.5f;//完整显示的时间
    [SerializeField, Min(0.01f)] private float errorFade = 1f;//淡出时间
    [SerializeField] private string nextScene = "MenuScene";
    [SerializeField] private TMP_FontAsset chineseFontAsset;//项目内的中文字体资产（Noto Sans SC）

    private static readonly Regex AccountPattern = new("^[a-zA-Z0-9_]{3,32}$");//与服务器的账号规则一致
    private const int MinPasswordLength = 8;
    private static TMP_FontAsset chineseFont;
    private bool registering, busy;
    private Coroutine fading;
    private int submittedFrame = -1;//同一帧里回车可能同时被输入框和键盘检测到，只提交一次

    private void Awake()
    {
        ApplyChineseFont();
        loginButton.onClick.AddListener(Login);
        registerButton.onClick.AddListener(() => SetMode(true));
        confirmRegisterButton.onClick.AddListener(Register);
        cancelButton.onClick.AddListener(() => SetMode(false));
        foreach (Button button in new[] { loginButton, registerButton, confirmRegisterButton, cancelButton })
            button.onClick.AddListener(ClearButtonSelection);//点过的按钮不保持选中，之后的回车只走 Submit
        password.contentType = TMP_InputField.ContentType.Password;
        account.onSubmit.AddListener(_ => Submit());//输入框里按回车
        password.onSubmit.AddListener(_ => Submit());
        SetMode(false);
        HideErrorImmediately();
    }

    private void Start() => account.ActivateInputField();

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || busy) return;
        if (keyboard.tabKey.wasPressedThisFrame)//Tab 在两个输入框之间切换
        {
            TMP_InputField next = account.isFocused ? password : account;
            next.ActivateInputField();
        }
        if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) Submit();//焦点不在输入框时按回车
    }

    private static void ClearButtonSelection()
    {
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null
            && EventSystem.current.currentSelectedGameObject.GetComponent<Button>() != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    private void Submit()//回车 = 点击当前模式左下角的按钮（登录模式点“登录”，注册模式点“注册”）
    {
        if (busy || submittedFrame == Time.frameCount) return;
        submittedFrame = Time.frameCount;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);//避免回车同时“按下”之前被选中的其他按钮
        if (registering) Register();
        else Login();
    }

    private void SetMode(bool register)
    {
        if (busy) return;
        registering = register;
        loginButton.gameObject.SetActive(!register);
        registerButton.gameObject.SetActive(!register);
        confirmRegisterButton.gameObject.SetActive(register);
        cancelButton.gameObject.SetActive(register);
        account.text = string.Empty;
        password.text = string.Empty;
        HideErrorImmediately();
        account.ActivateInputField();
    }

    private void Login()
    {
        if (busy || !TryReadInput(out string name, out string pass, out CloudSaveClient client)) return;
        SetBusy(true);
        client.LoginAccount(name, pass, (result, detail) =>
        {
            if (this == null) return;
            SetBusy(false);
            if (result == AccountResult.Success) EnterGame();
            else ShowError(result, detail);
        });
    }

    private void Register()
    {
        if (busy || !TryReadInput(out string name, out string pass, out CloudSaveClient client)) return;
        SetBusy(true);
        client.RegisterAccount(name, pass, (result, detail) =>
        {
            if (this == null) return;
            if (result != AccountResult.Success)
            {
                SetBusy(false);
                ShowError(result, detail);
                return;
            }
            //注册成功后直接用同一组账号密码登录
            client.LoginAccount(name, pass, (loginResult, loginDetail) =>
            {
                if (this == null) return;
                SetBusy(false);
                if (loginResult == AccountResult.Success) EnterGame();
                else ShowError(loginResult, loginDetail);
            });
        });
    }

    private bool TryReadInput(out string name, out string pass, out CloudSaveClient client)
    {
        name = account.text.Trim();
        pass = password.text;
        client = CloudSaveClient.Instance;
        if (name.Length == 0 || pass.Length == 0) return Fail("请输入账号和密码！");
        if (!AccountPattern.IsMatch(name)) return Fail("账号需为3～32位字母、数字或下划线！");
        if (pass.Length < MinPasswordLength) return Fail("密码至少8位！");
        if (client == null) return Fail("服务器未连接！");
        return true;
    }

    private bool Fail(string message)
    {
        ShowError(message);
        return false;
    }

    private void ShowError(AccountResult result, string detail)
    {
        string message = result switch
        {
            AccountResult.ServerUnavailable => "服务器未连接！",
            AccountResult.AccountNotFound => "账号不存在！",
            AccountResult.WrongPassword => "密码错误！",
            AccountResult.AccountExists => "账号已存在！",
            AccountResult.InvalidInput => "账号或密码格式不正确！",
            _ => "操作失败，请稍后重试！"
        };
        if (result == AccountResult.Failed) Debug.LogWarning($"[LoginPanel] {detail}");
        ShowError(message);
    }

    private void ShowError(string message)
    {
        if (errorText == null) return;
        if (fading != null) StopCoroutine(fading);
        errorText.text = message;
        errorText.alpha = 1f;//瞬间弹出
        fading = StartCoroutine(FadeError());
    }

    private IEnumerator FadeError()
    {
        yield return new WaitForSecondsRealtime(errorHold);
        for (float t = 0f; t < errorFade; t += Time.unscaledDeltaTime)
        {
            errorText.alpha = 1f - t / errorFade;
            yield return null;
        }
        errorText.alpha = 0f;
        fading = null;
    }

    private void HideErrorImmediately()
    {
        if (fading != null) StopCoroutine(fading);
        fading = null;
        if (errorText != null) errorText.alpha = 0f;
    }

    private void SetBusy(bool value)//请求期间禁止重复点击和输入
    {
        busy = value;
        foreach (Button button in new[] { loginButton, registerButton, confirmRegisterButton, cancelButton })
            if (button != null) button.interactable = !value;
        account.interactable = password.interactable = !value;
    }

    private void EnterGame()
    {
        if (!Application.CanStreamedLevelBeLoaded(nextScene))
        {
            Debug.LogError($"场景 {nextScene} 未加入构建列表");
            return;
        }
        SceneManager.LoadScene(nextScene);
    }

    private void ApplyChineseFont()//优先用项目内的中文字体资产；没指定时才临时用系统字体生成
    {
        TMP_FontAsset font = chineseFontAsset != null ? chineseFontAsset : GetChineseFont();
        if (font == null) return;
        foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true)) text.font = font;
        account.fontAsset = font;
        password.fontAsset = font;
    }

    private static TMP_FontAsset GetChineseFont()
    {
        if (chineseFont != null) return chineseFont;
        string[] families = { "Microsoft YaHei", "Microsoft YaHei UI", "微软雅黑", "SimHei", "Noto Sans CJK SC", "Arial Unicode MS" };
        foreach (string family in families)
        {
            chineseFont = TMP_FontAsset.CreateFontAsset(family, "Regular", 90);
            if (chineseFont != null) break;
        }
        return chineseFont;
    }
}
