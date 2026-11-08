using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PlayerButtonManager : MonoBehaviour
{
    [SerializeField] private GameObject bag, settings, skills, treasure;
    [Header("设置子页面")]
    [SerializeField] private GameObject settingsButtons, settingsVisual, intro;
    [Header("成对的开关按钮")]
    [SerializeField] private GameObject openAudio, closeAudio, openUI, closeUI;
    [Header("关闭 UI 时隐藏的 HUD（保留菜单入口）")]
    [SerializeField] private CanvasGroup[] hudGroups;
    private struct HudState { public float alpha; public bool interactable, blocksRaycasts; }
    private HudState[] hudStates;
    private bool uiVisible = true;
    private static float volumeBeforeMute = 1f;

    private void Awake()
    {
        ResolveSettingsReferences();
        UpdateInstructionLabel();
        hudStates = new HudState[hudGroups != null ? hudGroups.Length : 0];
        ShowSettingsContent();
        RefreshToggleButtons();
    }

    private void ResolveSettingsReferences()
    {
        if (settings == null) return;
        if (settingsButtons == null) settingsButtons = settings.transform.Find("buttons")?.gameObject;
        if (settingsVisual == null) settingsVisual = settings.transform.Find("visual")?.gameObject;
        if (intro == null) intro = settings.transform.Find("Intro")?.gameObject;
        if (openAudio == null) openAudio = settings.transform.Find("buttons/OpenAudio")?.gameObject;
        if (closeAudio == null) closeAudio = settings.transform.Find("buttons/CloseAudio")?.gameObject;
        if (openUI == null) openUI = settings.transform.Find("buttons/OpenUI")?.gameObject;
        if (closeUI == null) closeUI = settings.transform.Find("buttons/CloseUI")?.gameObject;
    }

    //快捷键：C 背包、V 技能、B 法宝、N 设置。界面已经开着时再按同一个键就关闭并继续游戏；开着别的界面时直接切换过去。
    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;
        if (keyboard.cKey.wasPressedThisFrame) Toggle(bag, OnBag);
        else if (keyboard.vKey.wasPressedThisFrame) Toggle(skills, OnSkills);
        else if (keyboard.bKey.wasPressedThisFrame) Toggle(treasure, OnTreasure);
        else if (keyboard.nKey.wasPressedThisFrame) Toggle(settings, OnSettings);
    }

    private void Toggle(GameObject panel, System.Action open)
    {
        if (panel == null) return;
        if (panel.activeSelf) BackGame();
        else open();
    }

    public void OnBag() => OpenPanel(bag);
    public void OnSettings()
    {
        ResolveSettingsReferences();
        ShowSettingsContent();
        RefreshToggleButtons();
        OpenPanel(settings);
    }
    public void OnSkills() => OpenPanel(skills);
    public void OnTreasure() => OpenPanel(treasure);

    public void CloseAudio()
    {
        if (AudioListener.volume > 0f) volumeBeforeMute = AudioListener.volume;
        AudioListener.volume = 0f;
        RefreshToggleButtons();
    }

    public void OpenAudio()
    {
        if (AudioListener.volume <= 0f) AudioListener.volume = volumeBeforeMute > 0f ? Mathf.Clamp01(volumeBeforeMute) : 1f;
        RefreshToggleButtons();
    }

    public void CloseUI()
    {
        if (uiVisible && hudGroups != null)
            for (int i = 0; i < hudGroups.Length; i++)
            {
                var group = hudGroups[i];
                if (group == null) continue;
                hudStates[i] = new HudState { alpha = group.alpha, interactable = group.interactable, blocksRaycasts = group.blocksRaycasts };
                group.alpha = 0f;
                group.interactable = group.blocksRaycasts = false;
            }
        uiVisible = false;
        RefreshToggleButtons();
    }

    public void OpenUI()
    {
        if (!uiVisible && hudGroups != null)
            for (int i = 0; i < hudGroups.Length; i++)
            {
                var group = hudGroups[i];
                if (group == null) continue;
                group.alpha = hudStates[i].alpha;
                group.interactable = hudStates[i].interactable;
                group.blocksRaycasts = hudStates[i].blocksRaycasts;
            }
        uiVisible = true;
        RefreshToggleButtons();
    }

    private void RefreshToggleButtons()
    {
        bool audible = AudioListener.volume > 0f;
        if (openAudio != null) openAudio.SetActive(!audible);
        if (closeAudio != null) closeAudio.SetActive(audible);
        if (openUI != null) openUI.SetActive(!uiVisible);
        if (closeUI != null) closeUI.SetActive(uiVisible);
    }

    public void OnIntro()
    {
        ResolveSettingsReferences();
        if (intro == null) return;
        OpenPanel(settings);
        if (settingsButtons != null) settingsButtons.SetActive(false);
        if (settingsVisual != null) settingsVisual.SetActive(false);
        intro.SetActive(true);
    }

    public void BackSettings() => OnSettings();

    private void ShowSettingsContent()
    {
        if (intro != null) intro.SetActive(false);
        if (settingsButtons != null) settingsButtons.SetActive(true);
        if (settingsVisual != null) settingsVisual.SetActive(true);
    }

    private void UpdateInstructionLabel()
    {
        RectTransform artwork = intro != null ? intro.transform.Find("ui") as RectTransform : null;
        if (artwork == null) return;
        var cover = new GameObject("CharacterSwitchInstructions", typeof(RectTransform), typeof(Image));
        cover.transform.SetParent(artwork, false);
        var rect = (RectTransform)cover.transform;
        rect.anchorMin = new Vector2(0.53f, 0.472f);
        rect.anchorMax = new Vector2(0.93f, 0.55f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var image = cover.GetComponent<Image>();
        image.color = new Color(0.071f, 0.068f, 0.068f);
        image.raycastTarget = false;
        CreateLabel(cover.transform, "1 / 2 / 3 / 4    切换出场角色", Vector2.zero, new Vector2(310f, 42f), 24);
    }

    private static Text CreateLabel(Transform parent, string title, Vector2 position, Vector2 size, int fontSize)
    {
        var label = new GameObject("Label", typeof(RectTransform), typeof(Text));
        label.transform.SetParent(parent, false);
        var rect = (RectTransform)label.transform;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        var text = label.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<UnityEngine.Font>("LegacyRuntime.ttf");
        label.AddComponent<RebirthText>();
        text.text = title;
        text.fontSize = fontSize;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(1f, 0.92f, 0.75f);
        text.raycastTarget = false;
        return text;
    }

    private void OpenPanel(GameObject panel)//同一时刻只显示一个暂停菜单，避免关闭一个后仍有另一个覆盖游戏
    {
        if (panel == null) return;
        if (bag != null && bag != panel) bag.SetActive(false);
        if (settings != null && settings != panel) settings.SetActive(false);
        if (skills != null && skills != panel) skills.SetActive(false);
        if (treasure != null && treasure != panel) treasure.SetActive(false);
        GamePause.Pause();//统一暂停游戏时间和角色输入，界面的鼠标操作继续可用
        panel.SetActive(true);
    }

    public void BackGame()//设置中的继续游戏只关闭菜单，不改变会话或存档
    {
        ClosePanels();
        GamePause.Resume();
    }

    public void BackMap()//返回地图保留GameSession中的临时金币、技能与经验，不触发保存
    {
        ClosePanels();
        BattleRun.Cancel();
        GoldManager.Current?.EndRun();//未完成吸附的金币不能在离开战斗后补入账
        GamePause.Reset();
        SceneManager.LoadScene("MapScene");
    }

    public void BackMenu()//主动退出到主菜单时丢弃本次未保存数据，下一次仍从上次存档继续
    {
        ClosePanels();
        BattleRun.Cancel();
        GoldManager.Current?.EndRun();
        GameSession.instance?.DiscardUnsavedProgress();
        GamePause.Reset();
        SceneManager.LoadScene("MenuScene");
    }

    private void ClosePanels()//恢复或切换场景时同时移除菜单遮挡，保持按钮已有的布局和引用
    {
        ShowSettingsContent();
        if (bag != null) bag.SetActive(false);
        if (settings != null) settings.SetActive(false);
        if (skills != null) skills.SetActive(false);
        if (treasure != null) treasure.SetActive(false);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetAudioCache() => volumeBeforeMute = AudioListener.volume > 0f ? AudioListener.volume : 1f;
}
