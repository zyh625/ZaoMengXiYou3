using TMPro;
using UnityEngine;

//背包左侧的角色信息：昵称、等级、属性、金币和当前角色的循环待机。
//打开背包时游戏处于暂停状态，属性不会变化，所以只在打开和存档数据变化时刷新一次。
public sealed class BagUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nickname;
    [SerializeField] private SpriteNumber level;
    [SerializeField] private SpriteNumber hp, mp, xp, attack, reduction, critical, hpRegen, mpRegen, gold;
    [SerializeField] private UISpriteLoop figure;//展示台里的角色待机动画

    private PlayerManager party;
    private PlayerController shownPlayer;

    private void OnEnable()
    {
        if (GameSession.instance != null) GameSession.instance.DataChanged += Refresh;
        EquipmentService.Changed += Refresh;
        shownPlayer = null;//重新打开时总是重新绑定待机帧，换过人也能正确显示
        Refresh();
    }

    private void OnDisable()
    {
        if (GameSession.instance != null) GameSession.instance.DataChanged -= Refresh;
        EquipmentService.Changed -= Refresh;
    }

    private void Refresh()
    {
        SaveData data = GameSession.instance != null ? GameSession.instance.data : null;
        string account = CloudSaveClient.Instance != null ? CloudSaveClient.Instance.Username : null;//昵称即登录账号名
        if (nickname != null) nickname.SetText(string.IsNullOrEmpty(account) ? "未登录" : account);
        gold?.SetValue(data != null ? Mathf.Max(0, data.gold) : 0);

        if (party == null) party = FindAnyObjectByType<PlayerManager>();
        PlayerController player = party != null ? party.CurrentPlayer : null;
        if (player == null) return;
        var health = player.GetComponent<Health>();
        var resources = player.GetComponent<PlayerResources>();
        var combat = player.GetComponent<PlayerCombat>();
        var stats = player.GetComponent<PlayerStats>();

        level?.SetValue(resources != null ? Mathf.Clamp(resources.Level, 1, 99) : 1);
        if (health != null) hp?.SetText(Ratio(health.CurBlood, health.BeginBlood));
        if (resources != null)
        {
            mp?.SetText(Ratio(resources.CurrentMana, resources.MaxMana));
            xp?.SetText(Ratio(resources.Experience, resources.ExperienceToNextLevel));
        }
        attack?.SetValue(Mathf.RoundToInt(100f * (combat != null ? combat.AttackMultiplier : 1f)));//基础100，含等级、武器和当前增益
        reduction?.SetText(Percent(health != null ? 1f - health.DamageTakenMultiplier : 0f));//易伤不显示负数
        critical?.SetText(Percent(combat != null ? combat.CriticalChance : 0f));
        hpRegen?.SetValue(Mathf.RoundToInt(stats != null ? stats.HPRegenPer5s : 0f));//每5秒恢复量
        mpRegen?.SetValue(Mathf.RoundToInt(stats != null ? stats.MPRegenPer5s : 0f));

        if (figure != null && player != shownPlayer && player.IdleAnimation != null)
        {
            shownPlayer = player;
            figure.SetFrames(player.IdleAnimation.sprites, Mathf.Max(1f, player.IdleAnimation.fps));
        }
    }

    private static string Ratio(float current, float maximum) => $"{(int)current}/{(int)maximum}";//与战斗血条的取整方式一致
    private static string Percent(float ratio) => $"{Mathf.RoundToInt(Mathf.Clamp01(ratio) * 100f)}%";
}
