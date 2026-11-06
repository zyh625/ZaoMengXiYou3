using UnityEngine.UI;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using System;
using System.Globalization;

public class MenuButton : MonoBehaviour
{
    [SerializeField] TMP_InputField inputName;
    [SerializeField] TextMeshProUGUI[] fileInformantion;//存档信息（昵称/时间/轮回共用一个文本）
    [SerializeField] private GameObject CreatFile;//创建存档界面，输入昵称
    [SerializeField] private GameObject selectUI;//存档界面
    [SerializeField] private GameObject coverUI;//存档界面
    [SerializeField] private GameObject deleteUI;//确认删除存档界面（SelectDelete）
    [SerializeField] private GameObject aboutGame;//关于游戏
    [SerializeField] private Button[] buttons;//新游戏开启第一个（覆盖），继续游戏开启第二个（删除）
    [SerializeField] private Button[] fileButtons;//存档按钮
    private bool[] haveFile = new bool[6];
    // 云端存档信息，服务器槽位从1开始
    private bool[] haveCloudFile = new bool[6];

    private CloudSaveClient.CloudSaveInfo[] cloudSaveInfos =
        new CloudSaveClient.CloudSaveInfo[6];

    private bool restoringCloudSave;
    private bool deletingFile;//等待云端删除结果期间不能再操作存档

    private const float DoubleClickTime = 0.4f;//两次点击同一存档的最大间隔
    private float lastClickTime = -1f;

    [Header("账号：切换与注销")]
    [SerializeField] private GameObject withdrawUI;//注销确认界面
    [SerializeField] private TMP_InputField withdrawPassword;
    [SerializeField] private TextMeshProUGUI withdrawError;//红色报错，出现后停留再淡出
    [SerializeField] private Button withdrawConfirmButton;
    [SerializeField, Min(0f)] private float errorHold = 1.5f;
    [SerializeField, Min(0.01f)] private float errorFade = 1f;
    [SerializeField] private string loginScene = "LoginScene";
    private bool withdrawing;//等待服务器注销结果期间不能重复提交或取消
    private System.Collections.IEnumerator fadeRoutine;

    private float alpha;
    private int fileIndex = -1;//选中存档
    private bool enteringGame;
    private bool creatingNew;//当前这次进入是新建/覆盖，不能被 haveFile 的旧值当成继续游戏
    private bool newGameMode;//新游戏时未使用的槽位也要显示为空按钮
    private static TMP_FontAsset chineseFont;//存档名允许中文，默认 TMP 字体没有中文字形
    private static string ShortTime(string value)//按钮内只显示“月-日 时:分”，给昵称留出字号
        => string.IsNullOrEmpty(value) || value.Length < 16 ? value : value.Substring(5, 11);

    private static string SlotText(string name, string time, int rebirth)//昵称、时间、轮回都放在同一个按钮文本里
    {
        string text = string.IsNullOrWhiteSpace(name) ? "" : name;
        if (!string.IsNullOrWhiteSpace(time))
            text = string.IsNullOrEmpty(text) ? ShortTime(time) : text + "\n" + ShortTime(time);
        if (rebirth > 0)
            text = string.IsNullOrEmpty(text) ? "第 " + rebirth + " 轮回" : text + "\n第 " + rebirth + " 轮回";
        return text;
    }

    private static TMP_FontAsset GetChineseFont()
    {
        if (chineseFont != null) return chineseFont;
        string[][] candidates =
        {
            new[] { "Microsoft YaHei", "Regular" }, new[] { "Microsoft YaHei", "Normal" },
            new[] { "Microsoft YaHei UI", "Regular" }, new[] { "微软雅黑", "Regular" },
            new[] { "SimHei", "Regular" }, new[] { "Noto Sans CJK SC", "Regular" },
            new[] { "Arial Unicode MS", "Regular" }
        };
        foreach (string[] candidate in candidates)
        {
            TMP_FontAsset font = TMP_FontAsset.CreateFontAsset(candidate[0], candidate[1], 90);
            if (font != null) { chineseFont = font; break; }
        }
        return chineseFont;
    }

    private void ApplyChineseFont()
    {
        TMP_FontAsset font = GetChineseFont();
        if (font == null) return;
        if (fileInformantion != null)
            foreach (TextMeshProUGUI text in fileInformantion)
                if (text != null) text.font = font;
        if (inputName != null) inputName.fontAsset = font;
    }

    void Start()
    {
        ApplyChineseFont();
        alpha = fileButtons[0].image.color.a;
        for(int i = 0; i < 6; i++)
        {
            Color c = fileButtons[i].image.color;
            c.a *= 0.8f;//开始都变淡
            fileButtons[i].image.color = c;
            SaveData data = SaveSystem.Load(i);
            if (data != null)
            {
                haveFile[i] = true;
                fileButtons[i].gameObject.SetActive(true);
                fileInformantion[i].SetText(SlotText(data.name, data.date, data.rebirth));
            }
            else
            {
                haveFile[i] = false;
                fileInformantion[i].SetText("");
            }

        }
        RefreshCloudSaveList();
    }


    private static string FormatCloudTime(string value)
    {
        if (DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out var time))
        {
            return time.ToLocalTime().ToString(
                "yyyy-MM-dd HH:mm:ss");
        }

        return value;
    }

    private void RefreshCloudSaveList()
    {
        if (CloudSaveClient.Instance == null)
        {
            Debug.LogError("CloudSaveClient 不存在，无法获取云端存档");
            return;
        }

        CloudSaveClient.Instance.GetSaveList((success, saves, message) =>
        {
            if (!success)
            {
                Debug.LogWarning($"获取云端存档失败：{message}");
                return;
            }

            Array.Clear(haveCloudFile, 0, haveCloudFile.Length);
            Array.Clear(cloudSaveInfos, 0, cloudSaveInfos.Length);

            if (saves != null)
            {
                foreach (var info in saves)
                {
                    // 服务器槽位1~6，对应本地槽位0~5
                    int index = info.slotNumber - 1;

                    if (index < 0 || index >= 6)
                        continue;

                    haveCloudFile[index] = true;
                    cloudSaveInfos[index] = info;
                }
            }

            // 刷新六个槽位的显示
            for (int i = 0; i < 6; i++)
            {
                SaveData localData = SaveSystem.Load(i);
                var cloudData = cloudSaveInfos[i];

                string saveTime = localData?.date ?? "";
                string saveName = localData?.name ?? "";
                int rebirth = localData != null ? localData.rebirth : 0;

                // 本地昵称为空时，尝试读取云端昵称
                if (cloudData != null)
                {
                    if (string.IsNullOrWhiteSpace(saveName))
                        saveName = cloudData.name;

                    // 本地时间为空时，使用云端更新时间
                    if (string.IsNullOrWhiteSpace(saveTime))
                        saveTime = FormatCloudTime(cloudData.updatedAtUtc);

                    // 没有本地存档时，使用云端轮回次数
                    if (localData == null)
                        rebirth = cloudData.rebirth;
                }

                fileInformantion[i].SetText(SlotText(saveName, saveTime, rebirth));
                if (string.IsNullOrWhiteSpace(saveName) && string.IsNullOrWhiteSpace(saveTime))
                    fileInformantion[i].SetText("");

                fileButtons[i].gameObject.SetActive(
                    localData != null || cloudData != null || newGameMode);
            }

            Debug.Log($"云端存档列表加载成功，共有 {saves.Length} 个存档");
        });
    }
    public void NewGame()//新游戏
    {
        newGameMode = true;
        selectUI.SetActive(true);
        TurnOn(true);
        buttons[0].gameObject.SetActive(true);
    }
    public void BackToMenu()//从存档界面返回主界面
    {
        creatingNew = false;
        buttons[0].gameObject.SetActive(false);
        buttons[1].gameObject.SetActive(false);
        selectUI.SetActive(false);aboutGame.SetActive(false);
        if (deleteUI != null) deleteUI.SetActive(false);
        Return();
    }
    public void EnsureCover()//确定覆盖存档
    {
        if (fileIndex == -1 || deletingFile) return;
        if (!haveFile[fileIndex] && !haveCloudFile[fileIndex])//如果本来为空，直接创建存档
        {
            creatingNew = true;
            CreatFile.SetActive(true);
        }
        else
            coverUI.SetActive(true);
    }
    public void BackToSelect()//从覆盖界面返回存档界面
    {
        creatingNew = false;
        coverUI.SetActive(false);
    }
    public void ContinueGame()//点击继续游戏
    {
        newGameMode = false;
        selectUI.SetActive(true);
        TurnOn(false);
        buttons[1].gameObject.SetActive(true);
    }
    public void ClickFile(int num)//单击选中存档，双击进入
    {
        if (deletingFile) return;

        float now = Time.unscaledTime;
        bool doubleClick = fileIndex == num && lastClickTime >= 0f
            && now - lastClickTime <= DoubleClickTime;
        lastClickTime = now;

        if (fileIndex == -1)
        {
            Color c = fileButtons[num].image.color;
            c.a = alpha;
            fileButtons[num].image.color = c;
        }
        else if (fileIndex != num)
        {
            Color c = fileButtons[fileIndex].image.color;
            c.a = fileButtons[num].image.color.a;
            fileButtons[fileIndex].image.color = c;
            c = fileButtons[num].image.color;
            c.a= alpha;
            fileButtons[num].image.color = c;
        }
        fileIndex = num;

        if (!doubleClick) return;
        lastClickTime = -1f;//第三次点击不再算作双击
        if (newGameMode) EnsureCover();//新游戏：双击等同点“覆盖”
        else EnterFile();//继续游戏：双击进入存档
    }

    public void ShowDeleteConfirm()//点击“删除”，弹出是否确定删除
    {
        if (deletingFile || fileIndex < 0 || fileIndex >= haveFile.Length) return;
        if (!haveFile[fileIndex] && !haveCloudFile[fileIndex]) return;//空槽没有可删的存档
        if (deleteUI == null)
        {
            Debug.LogError("MenuButton 未指定 deleteUI（SelectDelete）");
            return;
        }
        deleteUI.SetActive(true);
    }

    public void CancelDelete()//确认删除界面点“返回”
    {
        if (deletingFile) return;
        if (deleteUI != null) deleteUI.SetActive(false);
    }

    public void ConfirmDelete()//确认删除：先删云端，成功后再删本地，失败时两边都保留
    {
        if (deletingFile || fileIndex < 0 || fileIndex >= haveFile.Length) return;
        int slot = fileIndex;
        CloudSaveClient client = CloudSaveClient.Instance;

        if (client == null || !client.IsLoggedIn)
        {
            if (haveCloudFile[slot])
            {
                Debug.LogWarning("未登录，无法删除云端存档");
                return;
            }
            FinishDelete(slot);
            return;
        }

        deletingFile = true;
        client.DeleteSave(slot, (success, message) =>
        {
            deletingFile = false;
            if (this == null) return;//等待期间已切换场景

            if (!success)
            {
                Debug.LogWarning($"云端存档删除失败，本地存档已保留：{message}");
                return;
            }
            FinishDelete(slot);
        });
    }

    private void FinishDelete(int slot)
    {
        SaveSystem.Delete(slot);
        haveCloudFile[slot] = false;
        cloudSaveInfos[slot] = null;

        haveFile[slot] = SaveSystem.Load(slot) != null;
        if (haveFile[slot])
            Debug.LogWarning($"本地存档槽位 {slot} 删除失败，请检查文件是否被占用");
        else
            fileInformantion[slot].SetText("");

        if (deleteUI != null) deleteUI.SetActive(false);
        if (fileIndex == slot) Return();
        fileButtons[slot].gameObject.SetActive(haveFile[slot] || newGameMode);
        Debug.Log($"存档槽位 {slot} 已删除");
    }

    public void EnterFile()//确定进入存档
    {
        if (fileIndex == -1 || deletingFile) return;
        if (haveFile[fileIndex] || haveCloudFile[fileIndex])//已创建的
            StartGame();
        else//新创建
        {
            creatingNew = true;
            CreatFile.SetActive(true);
        }
    }
    public void CoverFile()//确定覆盖存档了
    {
        fileButtons[fileIndex].gameObject.SetActive(false);
        SaveSystem.Delete(fileIndex);
        haveFile[fileIndex] = false;//覆盖后按新存档读取输入昵称，不能仍当作已存在的旧档进入
        creatingNew = true;
        CreatFile.SetActive(true);
    }
    public void SwitchAccount()//切换账号：退出当前登录、清空会话，回到登录界面
    {
        if (withdrawing || enteringGame || restoringCloudSave || deletingFile) return;
        LeaveAccount();
    }

    public void ShowWithdraw()//注销账号：先弹出确认界面，要求再次输入密码
    {
        if (withdrawUI == null || enteringGame || deletingFile) return;
        withdrawUI.SetActive(true);
        withdrawPassword.text = string.Empty;
        HideWithdrawError();
        withdrawPassword.ActivateInputField();
    }

    public void CancelWithdraw()//取消注销，回到主菜单
    {
        if (withdrawing) return;
        HideWithdrawError();
        if (withdrawUI != null) withdrawUI.SetActive(false);
    }

    public void ConfirmWithdraw()
    {
        if (withdrawing) return;
        string password = withdrawPassword.text;
        if (password.Length == 0)
        {
            ShowWithdrawError("请输入密码！");
            return;
        }
        CloudSaveClient client = CloudSaveClient.Instance;
        if (client == null)
        {
            ShowWithdrawError("服务器未连接！");
            return;
        }

        SetWithdrawing(true);
        client.DeleteAccount(password, (result, detail) =>
        {
            if (this == null) return;
            SetWithdrawing(false);
            if (result == CloudSaveClient.AccountResult.Success)
            {
                LeaveAccount();
                return;
            }
            if (result == CloudSaveClient.AccountResult.Failed) Debug.LogWarning($"注销账号失败：{detail}");
            ShowWithdrawError(result switch
            {
                CloudSaveClient.AccountResult.WrongPassword => "密码错误！",
                CloudSaveClient.AccountResult.ServerUnavailable => "服务器未连接！",
                CloudSaveClient.AccountResult.SessionExpired => "登录已过期，请重新登录！",
                CloudSaveClient.AccountResult.AccountNotFound => "账号不存在！",
                _ => "注销失败，请稍后重试！"
            });
        });
    }

    private void LeaveAccount()
    {
        CloudSaveClient.Instance?.Logout();
        if (GameSession.instance != null) GameSession.instance.ResetForAccountChange();
        if (!Application.CanStreamedLevelBeLoaded(loginScene))
        {
            Debug.LogError($"场景 {loginScene} 未加入构建列表");
            return;
        }
        enteringGame = true;
        SceneManager.LoadScene(loginScene);
    }

    private void SetWithdrawing(bool value)
    {
        withdrawing = value;
        if (withdrawConfirmButton != null) withdrawConfirmButton.interactable = !value;
        if (withdrawPassword != null) withdrawPassword.interactable = !value;
    }

    private void ShowWithdrawError(string message)//瞬间弹出，停留后逐渐淡出
    {
        if (withdrawError == null) return;
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        withdrawError.text = message;
        withdrawError.alpha = 1f;
        fadeRoutine = FadeWithdrawError();
        StartCoroutine(fadeRoutine);
    }

    private System.Collections.IEnumerator FadeWithdrawError()
    {
        yield return new WaitForSecondsRealtime(errorHold);
        for (float t = 0f; t < errorFade; t += Time.unscaledDeltaTime)
        {
            withdrawError.alpha = 1f - t / errorFade;
            yield return null;
        }
        withdrawError.alpha = 0f;
        fadeRoutine = null;
    }

    private void HideWithdrawError()
    {
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = null;
        if (withdrawError != null) withdrawError.alpha = 0f;
    }

    public void AboutGame()=>aboutGame.SetActive(true);//关于游戏
    public void ExitGame()=>Application.Quit();//退出游戏
    public void StartGame()
    {
        if (enteringGame || restoringCloudSave || deletingFile ||
        fileIndex < 0 || fileIndex >= haveFile.Length)
            return;

        // 本地没有、云端存在：先恢复云端存档；覆盖/新建时不恢复，直接创建新档
        if (!creatingNew && !haveFile[fileIndex] && haveCloudFile[fileIndex])
        {
            RestoreCloudSaveAndStart(fileIndex);
            return;
        }

        bool creating = creatingNew || CreatFile.activeSelf || !haveFile[fileIndex];

        string destination = creating ? "FirstScene" : "MapScene";
        if (!Application.CanStreamedLevelBeLoaded(destination))
        {
            Debug.LogError($"场景 {destination} 未加入构建列表");
            return;
        }

        string name = creating ? inputName.text.Trim() : string.Empty;
        if (creating && (string.IsNullOrWhiteSpace(name) || name.Length > 12 || CheckName(name))) return;

        if (creating) SaveSystem.Delete(fileIndex);//覆盖/新建前先清掉旧档，SelectFile 才会用新昵称创建
        GameSession session = GameSession.EnsureSession();
        session.SelectFile(fileIndex, name);
        creatingNew = false;

        if (creating)
        {
            session.battle.curLevel = 1;
            if (!SaveSystem.TrySave()) return;
            haveFile[fileIndex] = true;
        }

        enteringGame = true;
        session.priScene = "MenuScene";
        SceneManager.LoadScene(destination);
    }

    private void RestoreCloudSaveAndStart(int slot)
    {
        if (restoringCloudSave || enteringGame)
            return;

        CloudSaveClient client = CloudSaveClient.Instance;

        if (client == null)
        {
            Debug.LogError("CloudSaveClient 不存在，无法恢复云端存档");
            return;
        }

        restoringCloudSave = true;

        client.DownloadSave(slot, (success, data, message) =>
        {
            restoringCloudSave = false;

            if (!success || data == null)
            {
                Debug.LogError($"云端存档下载失败：{message}");
                return;
            }

            // 写入本地存档，并保留原有备份机制
            if (!SaveSystem.TryRestoreToSlot(slot, data))
            {
                Debug.LogError("云端存档下载成功，但本地恢复失败");
                return;
            }

            haveFile[slot] = true;

            fileButtons[slot].gameObject.SetActive(true);



            fileInformantion[slot].SetText(SlotText(data.name, data.date, data.rebirth) + "\n云端已恢复");

            Debug.Log($"云端存档已恢复到本地槽位 {slot}");

            // 如果玩家在下载期间改变了选择，不自动进入游戏
            if (fileIndex != slot)
            {
                Debug.Log("当前选择已改变，请重新选择存档");
                return;
            }

            // 恢复成功，重新调用原有进入游戏流程
            StartGame();
        });
    }

    private void TurnOn(bool b)
    {
        for (int i = 0; i < 6; i++)
        {
            if (b)
            {
                // 新游戏：允许选择没有本地存档的槽位
                if (!haveFile[i])
                    fileButtons[i].gameObject.SetActive(true);
            }
            else
            {
                // 继续游戏：本地或云端存在存档即可显示
                fileButtons[i].gameObject.SetActive(
                    haveFile[i] || haveCloudFile[i]);
            }
        }
    }
    private bool CheckName(string n)
    {
        for(int i = 0; i < 6; i++)
        {
            if (haveFile[i] && SaveSystem.Load(i)?.name == n) return true;
        }
        return false;
    }
    private void Return()
    {
        if (fileIndex == -1) return;
        Color c = fileButtons[fileIndex].image.color;
        c.a = alpha * 0.5f;
        fileButtons[fileIndex].image.color = c;
        fileIndex = -1;
    }
}
