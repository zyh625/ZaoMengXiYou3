using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [SerializeField] private Image[] playerStates;//血量、蓝量、经验、气力
    [SerializeField] private Image[] playerAngry;//无双能量槽
    [SerializeField]private TextMeshProUGUI[] texts;
    [SerializeField] private Image portrait;//场景中当前角色的头像Image
    [SerializeField] private Image level1, level2;//头像下等级的十位和个位，个位数时只显示level2
    [SerializeField] private Image[] skillIcons = new Image[3];//UIO对应的技能图标，仅替换sprite，不改现有布局
    [SerializeField] private Image[] skillBlocks = new Image[3];//UIO对应的冷却遮罩，通过剩余冷却比例更新fillAmount
    [SerializeField] private CharacterSlot[] characterSlots;

    [System.Serializable]
    private sealed class CharacterSlot
    {
        public Image portrait = null, cooldown = null;
        [System.NonSerialized] public Sprite originalSprite;
        [System.NonSerialized] public Color originalColor;
        [System.NonSerialized] public Outline activeOutline;
        [System.NonSerialized] public TextMeshProUGUI timer;
        [System.NonSerialized] public int displayedSeconds;
    }

    private PlayerUI player;
    private PlayerManager party;
    private PlayerController currentPlayer;//停用UI后重新启用时恢复当前角色的绑定
    private Health health;//只订阅当前角色，备用角色的属性变化不能覆盖界面
    private PlayerResources resources;//法力和经验共用角色资源组件的变化通知
    private PlayerBoost boost;//只订阅当前角色的无双能量，避免切换后旧角色覆盖能量条
    private PlayerCombat combat;//技能冷却仍由各角色保存，界面只读取当前角色的招式数据
    private float[] previousSkillRatios;//记录当前绑定期间实际观察到的冷却，刚切入已就绪技能不闪烁
    private int[] completionFlashFrames;//结束闪烁保持一个渲染帧，下一帧才把遮罩重新清零

    private void Awake()
    {
        if (characterSlots == null) return;
        foreach (CharacterSlot slot in characterSlots)
        {
            if (slot?.portrait == null || slot.cooldown == null) continue;
            slot.originalSprite = slot.portrait.sprite;
            slot.originalColor = slot.portrait.color;
            slot.activeOutline = slot.portrait.gameObject.AddComponent<Outline>();
            slot.activeOutline.effectColor = new Color(1f, 0.79f, 0.28f, 1f);
            slot.activeOutline.effectDistance = new Vector2(1.5f, 1.5f);
            slot.activeOutline.enabled = false;

            GameObject counter = new("CooldownSeconds", typeof(RectTransform), typeof(TextMeshProUGUI));
            var rect = (RectTransform)counter.transform;
            rect.SetParent(slot.cooldown.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            slot.timer = counter.GetComponent<TextMeshProUGUI>();
            slot.timer.font = TMP_Settings.defaultFontAsset;
            slot.timer.fontSize = 18f;
            slot.timer.alignment = TextAlignmentOptions.Center;
            slot.timer.raycastTarget = false;
            counter.SetActive(false);
        }
    }

    public void BindPlayer(PlayerController curPlayer, PlayerManager manager = null)//换人时在同一个入口切换图片、数据与订阅
    {
        UnbindPlayer();
        if (manager != null) party = manager;
        currentPlayer = curPlayer;
        if (curPlayer == null) return;
        player = curPlayer.GetComponent<PlayerUI>();
        health = curPlayer.GetComponent<Health>();
        resources = curPlayer.GetComponent<PlayerResources>();
        boost = curPlayer.GetComponent<PlayerBoost>();
        combat = curPlayer.GetComponent<PlayerCombat>();
        if (!isActiveAndEnabled) return;

        InitPlayer();
        if (health != null) health.HealthChanged += OnHealthChanged;
        if (resources != null) resources.Changed += RefreshResources;
        if (boost != null) boost.Changed += RefreshBoost;
        if (combat != null) combat.SkillCooldownStarted += OnSkillCooldownStarted;
    }

    public void SetPlayer(PlayerController curPlayer) => BindPlayer(curPlayer);//保留旧关卡入口
    public void InitPlayer()
    {
        RefreshCharacterBar();
        RefreshSkillBar();//换人时立即显示新角色图标与原有冷却剩余时间
        if (player == null) return;
        player.SetUI(playerStates, texts, playerAngry);
        player.SetIdentityUI(portrait);//只替换Canvas下Head的Image.sprite，保留原状态栏布局
        if (health != null) OnHealthChanged(health.CurBlood, health.BeginBlood);
        RefreshResources();
        RefreshBoost();
    }

    private void OnHealthChanged(float current, float maximum) => player?.UpdateHP(current, maximum);

    private void RefreshResources()//切入角色时立即刷新，之后由资源变化事件更新
    {
        if (player == null || resources == null) return;
        player.UpdateMP(resources.CurrentMana, resources.MaxMana);
        player.UpdateXP(resources.Experience, resources.ExperienceToNextLevel);
        player.UpdateVigor(resources.CurrentVigor, resources.MaxVigor);
        RefreshLevel(resources.Level);//换人和等级变化沿用资源事件同步，不单独逐帧读取
    }

    private void RefreshLevel(int level)//只限制两位数字的显示范围，不修改角色真实等级或存档
    {
        level = Mathf.Clamp(level, 1, 99);
        if (level1 != null)
        {
            level1.sprite = Font.GetSprite(level / 10);
            level1.enabled = true;
            level1.gameObject.SetActive(level >= 10);//十位不足时隐藏整个数字物体
        }
        if (level2 != null)
        {
            level2.sprite = Font.GetSprite(level % 10);
            level2.enabled = true;
            level2.gameObject.SetActive(true);//始终从原来的个位位置显示，1级不补前导零
        }
    }

    private void UnbindPlayer()//移除旧订阅，避免多次切换后重复通知或写入其他角色的血条
    {
        if (health != null) health.HealthChanged -= OnHealthChanged;
        if (resources != null) resources.Changed -= RefreshResources;
        if (boost != null) boost.Changed -= RefreshBoost;
        if (combat != null) combat.SkillCooldownStarted -= OnSkillCooldownStarted;
        if (player != null) player.ClearUI();
        player = null;
        health = null;
        resources = null;
        boost = null;
        combat = null;
    }

    private void RefreshBoost()//换人立即显示新角色的能量和满能量提示，之后按事件更新
    {
        if (player == null) return;
        player.UpdateBoost(boost != null ? boost.Energy : 0f,
            boost != null ? boost.MaxEnergy : 0f, boost != null && boost.Active);
    }

    private void OnEnable()
    {
        KeyBindings.Changed += RefreshSkillBar;
        if (currentPlayer != null) BindPlayer(currentPlayer);
    }

    private void OnDisable()
    {
        KeyBindings.Changed -= RefreshSkillBar;
        UnbindPlayer();
    }

    private void RefreshCharacterBar()
    {
        if (party == null || characterSlots == null) return;
        var order = party.SwitchOrder;
        if (characterSlots.Length != order.Count) return;
        for (int i = 0; i < characterSlots.Length; i++)
        {
            CharacterSlot slot = characterSlots[i];
            if (slot?.timer == null) continue;
            PlayerId id = order[i];
            PlayerController character = party.GetCharacter(id);
            GameObject slotRoot = slot.portrait.transform.parent != null ? slot.portrait.transform.parent.gameObject : slot.portrait.gameObject;
            if (slotRoot.activeSelf != (character != null)) slotRoot.SetActive(character != null);//未解锁的角色不创建实例，对应槽位整体隐藏
            if (character == null) continue;
            bool available = character != null && !character.GetComponent<Health>().Dead;
            bool active = available && character == party.CurrentPlayer;
            float remaining = party.GetSwitchCooldownRemaining(id, out float ratio);
            int seconds = available && !active ? Mathf.CeilToInt(remaining) : 0;

            slot.portrait.sprite = characterSlots[(int)id]?.originalSprite;
            slot.portrait.color = available ? slot.originalColor : slot.originalColor * new Color(0.4f, 0.4f, 0.4f, 1f);
            slot.activeOutline.enabled = active;
            slot.cooldown.fillAmount = seconds > 0 ? ratio : 0f;
            if (slot.displayedSeconds == seconds) continue;
            slot.displayedSeconds = seconds;
            slot.timer.gameObject.SetActive(seconds > 0);
            if (seconds > 0) slot.timer.SetText("{0}", seconds);
        }
    }

    private void RefreshSkillBar()//仅配置技能遮罩的填充方式，不改变尺寸、位置、背景或材质
    {
        if (skillIcons != null)
            for (int i = 0; i < skillIcons.Length; i++)
                if (skillIcons[i] != null)
                    skillIcons[i].sprite = combat != null ? combat.GetSkillIcon(SlotSkill(i)) : null;//显示该按键绑定的技能
        int count = skillBlocks != null ? skillBlocks.Length : 0;//显示槽数由场景配置决定，与角色技能总数分开
        previousSkillRatios = new float[count];
        completionFlashFrames = new int[count];
        for (int i = 0; i < count; i++)
        {
            completionFlashFrames[i] = -1;
            float ratio = combat != null ? combat.GetSkillCooldownRatio(SlotSkill(i)) : 0f;//首次绑定直接同步，不把已就绪当作刚冷却结束
            previousSkillRatios[i] = ratio;
            Image block = skillBlocks[i];
            if (block == null) continue;
            block.type = Image.Type.Filled;
            block.fillMethod = Image.FillMethod.Vertical;
            block.fillOrigin = (int)Image.OriginVertical.Bottom;//剩余阴影留在底部，减少时从顶部向下露出技能图标
            block.fillAmount = combat != null && !combat.IsSkillLearned(SlotSkill(i)) ? 1f : ratio;//未学习的技能保持遮挡，购买成功后立即可以进入正常冷却显示
        }
    }
    private void OnSkillCooldownStarted(int k)//真正接受技能立即盖满，不等待下一次冷却比例轮询；绑定同一技能的按键一起盖满
    {
        if (skillBlocks == null) return;
        if (previousSkillRatios == null || previousSkillRatios.Length != skillBlocks.Length) RefreshSkillBar();
        for (int index = 0; index < skillBlocks.Length; index++)
        {
            if (SlotSkill(index) != k) continue;
            previousSkillRatios[index] = 1f;
            completionFlashFrames[index] = -1;
            if (skillBlocks[index] != null) skillBlocks[index].fillAmount = 1f;
        }
    }

    private int SlotSkill(int slot) => KeyBindings.SkillFor(resources != null ? resources.Id : PlayerId.WuKong, slot + 1);//技能栏第slot格对应的技能编号
    private void LateUpdate()//使用渲染帧更新倒计时，冷却完成的闪烁才会真正显示一帧
    {
        RefreshCharacterBar();
        if (combat == null || skillBlocks == null) return;
        if (previousSkillRatios == null || previousSkillRatios.Length != skillBlocks.Length) RefreshSkillBar();
        for (int i = 0; i < skillBlocks.Length; i++)
        {
            Image block = skillBlocks[i];
            if (!combat.IsSkillLearned(SlotSkill(i)))
            {
                if (block != null) block.fillAmount = 1f;
                previousSkillRatios[i] = 0f;//未学遮挡不算技能冷却，学习时不会误触发冷却完成闪烁
                completionFlashFrames[i] = -1;
                continue;
            }
            float ratio = combat.GetSkillCooldownRatio(SlotSkill(i));//备用角色的时间在战斗组件里继续推进，切回时直接读取
            if (ratio > 0f)
            {
                completionFlashFrames[i] = -1;
                if (block != null) block.fillAmount = ratio;
            }
            else if (previousSkillRatios[i] > 0f)
            {
                completionFlashFrames[i] = Time.frameCount;//本帧保持满遮罩供Canvas渲染，下一帧再清空
                if (block != null) block.fillAmount = 1f;
            }
            else if (completionFlashFrames[i] >= 0 && Time.frameCount <= completionFlashFrames[i])
            {
                if (block != null) block.fillAmount = 1f;
            }
            else
            {
                completionFlashFrames[i] = -1;
                if (block != null) block.fillAmount = 0f;
            }
            previousSkillRatios[i] = ratio;
        }
    }
}
