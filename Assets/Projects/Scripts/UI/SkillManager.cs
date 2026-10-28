using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SkillManager : MonoBehaviour
{
    [SerializeField] private GameObject[] Skills;//猴，猪，沙，唐
    private int curCharactor = 0;//默认打开猴子的技能面板
    [SerializeField] private Sprite[] numberSprites = new Sprite[10];//Numbers图片中按0到9排列的数字，保留费用和等级原来的字体
    [SerializeField] private Transform curGold;//当前金币区域，使用其goldNum下已有的数字图片
    private SkillNumberDisplay goldDisplay;//缓存余额数字，和技能购买按钮共用数据变化通知
    private readonly List<PurchaseButton> purchaseButtons = new();//只给已有学习、升级按钮接逻辑，不创建或移动UI
    private readonly List<SkillView> skillViews = new();//缓存每个技能的遮罩与数值，不在刷新时反复遍历层级
    private GameSession session;//只在界面启用期间订阅金币与技能数据变化
    private bool initialized;//反复打开界面也只给购买按钮绑定一次

    private sealed class PurchaseButton//按钮与实际显示角色绑定，重复占位面板不能误购另一个角色的技能
    {
        public Button button;
        public PlayerId player;
        public int skillNumber;
        public bool upgrade;
        public UnityAction callback;//销毁界面时仅移除自己注册的事件，保留Inspector原有事件
    }

    private sealed class SkillView//一个技能的显示引用，角色和技能编号与战斗及存档保持一致
    {
        public PlayerId player;//所属角色
        public int skillNumber;//从1开始的技能编号
        public GameObject block;//未学习时显示，学习成功即隐藏
        public SkillNumberDisplay cost, mana, cooldown, level;//沿用原Image数字与冷却的秒单位
    }

    private void Start() => Initialize();

    private void OnEnable()
    {
        Initialize();
        BindSession();
        ShowCurrentPanel();
        RefreshButtons();
    }

    private void Initialize()//首次打开与直接运行技能场景都能接好按钮，不依赖Start的先后顺序
    {
        if (initialized) return;
        initialized = true;
        if (curGold == null) curGold = transform.Find("curGold");//兼容未在Inspector填写的新技能面板
        Transform goldNum = curGold != null ? curGold.Find("goldNum") : null;//intro是固定提示图片，不参与数字更新
        if (goldNum != null)
            goldDisplay = new SkillNumberDisplay(goldNum, 7, numberSprites, firstDigit: 1);//固定使用num1到num7，从第一位开始显示
        BindPurchaseButtons();
    }

    private void BindSession()//关卡内拾取金币或在其他界面学习后，已打开的面板也能立即刷新
    {
        GameSession current = GameSession.EnsureSession();
        if (session == current) return;
        if (session != null) session.DataChanged -= RefreshButtons;
        session = current;
        if (isActiveAndEnabled) session.DataChanged += RefreshButtons;
    }

    private void OnDisable()
    {
        if (session != null) session.DataChanged -= RefreshButtons;
        session = null;
    }

    private void OnDestroy()
    {
        foreach (PurchaseButton binding in purchaseButtons)
            if (binding.button != null && binding.callback != null)
                binding.button.onClick.RemoveListener(binding.callback);
    }
    public void PressCharactor(int num)//点击哪个角色就打开哪个面板
    {
        if (Skills == null || num < 0 || num >= Skills.Length || Skills[num] == null) return;
        Initialize();
        curCharactor = num;
        ShowCurrentPanel();
        RefreshButtons();
    }
    public void PressBack()//战斗中从技能菜单返回时恢复游戏，地图中关闭面板则不改变未保存数据
    {
        gameObject.SetActive(false);
        GamePause.Resume();
    }

    public void Learn(int skillNumber)//以后新面板也可以直接通过Inspector绑定技能编号1、2、3
    {
        TryLearn((PlayerId)curCharactor, skillNumber);
    }

    public void Upgrade(int skillNumber)
    {
        TryUpgrade((PlayerId)curCharactor, skillNumber);
    }

    public bool TryLearn(PlayerId player, int skillNumber)//购买只更新当前会话，统一由地图保存按钮或胜败结算写入存档
    {
        bool learned = GameSession.EnsureSession().TryLearnSkill(player, skillNumber);
        RefreshButtons();
        return learned;
    }

    public bool TryUpgrade(PlayerId player, int skillNumber)
    {
        bool upgraded = GameSession.EnsureSession().TryUpgradeSkill(player, skillNumber);
        RefreshButtons();
        return upgraded;
    }

    private void ShowCurrentPanel()//场景中存在重复引用时只处理一次，不让后一个重复项把当前面板再次关闭
    {
        if (Skills == null || curCharactor >= Skills.Length) return;
        HashSet<GameObject> shown = new();
        foreach (GameObject panel in Skills)
            if (panel != null && shown.Add(panel)) panel.SetActive(panel == Skills[curCharactor]);
    }

    private void BindPurchaseButtons()//按实际技能对象绑定，角色选择和返回按钮继续使用原Inspector事件
    {
        if (Skills == null) return;
        HashSet<GameObject> bound = new();
        for (int i = 0; i < Skills.Length; i++)
        {
            GameObject panel = Skills[i];
            if (panel == null || !bound.Add(panel)) continue;
            PlayerId player = PanelPlayer(panel.name, (PlayerId)i);
            foreach (Transform row in panel.transform)
            {
                int number = SkillNumber(row.name, player);
                if (number < 1) continue;
                Transform waitTime = row.Find("WaitTime");//num2是“秒”的图片，只更新前两个数字
                SkillView view = new()
                {
                    player = player,
                    skillNumber = number,
                    block = row.Find("Block")?.gameObject,
                    cost = new SkillNumberDisplay(row.Find("cost"), 3, numberSprites),
                    mana = new SkillNumberDisplay(row.Find("MP"), 3, numberSprites),
                    cooldown = new SkillNumberDisplay(waitTime, 2, numberSprites, waitTime != null ? waitTime.Find("num2") : null),
                    level = new SkillNumberDisplay(row.Find("Level"), 1, numberSprites)
                };
                skillViews.Add(view);
                BindButton(row.Find("Learn")?.GetComponent<Button>(), player, number, false);
                BindButton(row.Find("UpGrade")?.GetComponent<Button>(), player, number, true);
            }
        }
    }

    private void BindButton(Button button, PlayerId player, int skillNumber, bool upgrade)//已有购买持久事件时不重复扣费
    {
        if (button == null) return;
        PurchaseButton binding = new() { button = button, player = player, skillNumber = skillNumber, upgrade = upgrade };
        purchaseButtons.Add(binding);
        string method = upgrade ? nameof(Upgrade) : nameof(Learn);//只排除同一个管理器的原购买事件，不影响音效等事件
        for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
            if (button.onClick.GetPersistentTarget(i) == this && button.onClick.GetPersistentMethodName(i) == method) return;
        binding.callback = () =>
        {
            if ((int)binding.player != curCharactor) return;
            if (binding.upgrade) TryUpgrade(binding.player, binding.skillNumber);
            else TryLearn(binding.player, binding.skillNumber);
        };
        button.onClick.AddListener(binding.callback);
    }

    private void RefreshButtons()//同一份等级配置决定显示和实战属性，不修改UI图片布局
    {
        SaveData data = GameSession.EnsureSession().EnsureRuntimeData();
        goldDisplay?.SetValue(Mathf.Clamp(data.gold, 0, 9999999));//余额区域最多显示7位，超出时显示9999999，不改实际存档金币
        SkillProgression progression = SkillProgression.Current;//统一读取每个角色、技能、等级的数据
        foreach (SkillView view in skillViews)
        {
            int level = data.GetPlayer(view.player).GetSkillLevel(view.skillNumber);
            bool maxed = level >= progression.MaxLevel;//满级只展示当前属性，升级按钮保持显示但不可点击
            SkillLevelData current = progression.GetLevelData(view.player, view.skillNumber, Mathf.Max(1, level));
            SkillLevelData next = progression.GetLevelData(view.player, view.skillNumber, level + 1);
            if (view.block != null) view.block.SetActive(level == 0);
            view.level.SetValue(level);
            view.mana.SetValue(current.manaCost);
            view.cooldown.SetValue(current.cooldown);
            view.cost.SetValue(maxed ? 0 : next.goldCost);//显示下一次购买所需金币，满级不再需要金币
        }
        foreach (PurchaseButton binding in purchaseButtons)
        {
            if (binding.button == null) continue;
            int level = data.GetPlayer(binding.player).GetSkillLevel(binding.skillNumber);
            int cost = progression.GetLevelData(binding.player, binding.skillNumber, level + 1).goldCost;
            binding.button.gameObject.SetActive(binding.upgrade ? level > 0 : level == 0);
            binding.button.interactable = (int)binding.player == curCharactor
                && data.gold >= cost && level < progression.MaxLevel;
        }
    }

    private static PlayerId PanelPlayer(string name, PlayerId fallback)//识别已有实际面板，兼容场景暂时重复填充的Skills数组
    {
        if (name.IndexOf("wukong", StringComparison.OrdinalIgnoreCase) >= 0) return PlayerId.WuKong;
        if (name.IndexOf("bajie", StringComparison.OrdinalIgnoreCase) >= 0) return PlayerId.BaJie;
        if (name.IndexOf("sha", StringComparison.OrdinalIgnoreCase) >= 0) return PlayerId.Sha;
        if (name.IndexOf("tang", StringComparison.OrdinalIgnoreCase) >= 0) return PlayerId.Tang;
        return fallback;
    }

    private static int SkillNumber(string name, PlayerId player)//本体技能编号与当前战斗映射保持一致，唐僧水柱为二、冰阵为三
    {
        if (name.StartsWith("Skill", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(name.Substring(5), out int number)) return number;
        switch (player)
        {
            case PlayerId.WuKong: return name == "FlameFlash" ? 1 : name == "HeavySlash" ? 2 : name == "AirSlash" ? 3 : 0;
            case PlayerId.BaJie: return name == "HolyShield" ? 1 : name == "PeakDevourer" || name == "PeakDeavourer" ? 2 : name == "TerraSanctum" ? 3 : 0;
            case PlayerId.Sha: return name == "ParalysisPotion" ? 1 : name == "SoulReaping" ? 2 : name == "QuickSand" ? 3 : 0;
            case PlayerId.Tang: return name == "GoldenShield" || name == "Protection" ? 1 : name == "OceanBreaker" ? 2 : name == "FrostFormation" ? 3 : 0;
            default: return 0;
        }
    }
}
