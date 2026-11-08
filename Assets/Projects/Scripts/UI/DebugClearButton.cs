using UnityEngine;
using UnityEngine.SceneManagement;

//【临时调试】地图上的 ClearButton：当前轮回直接通关并解锁全部角色，发放全部法宝，四人各穿上最高档传说武器、防具和饰品；
//之后进入的关卡里所有角色无敌（不扣血）、技能与换人无冷却、法力与气力无消耗，并且一击必杀。
//调试完成后删除本文件和地图上的 ClearButton 即可，其他代码不依赖这里。
public sealed class DebugClearButton : MonoBehaviour
{
    private const int LastLevel = 9;
    private const int AllCharacters = 15;//悟空/八戒/沙僧/唐僧 的解锁位

    private void Awake()//正式包里隐藏这个调试按钮；编辑器和勾选 Development Build 的测试包仍然可用
    {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
        gameObject.SetActive(false);
#endif
    }

    public void ClearCurrentRebirth()
    {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
        return;
#endif
        GameSession session = GameSession.instance;
        if (session == null || !session.HasSelectedFile || session.data == null)
        {
            Debug.LogWarning("[调试] 请先从主菜单选择存档再使用通关按钮");
            return;
        }

        SaveData data = session.data;
        data.gameLevel = LastLevel;//第 1～9 关全部可进入
        data.unlocked = AllCharacters;
        data.rebirthAvailable = true;//视为本轮已通第 9 关，地图上可再入轮回
        session.maxLevel = data.gameLevel;
        int skipped = EquipmentService.DebugGrantAll(data);//全部法宝 + 四人最高档传说武器、防具、饰品
        if (skipped > 0) Debug.LogWarning($"[调试] 背包已满，有 {skipped} 件法宝没有放下");
        session.NotifyDataChanged();
        if (!SaveSystem.TrySave()) Debug.LogWarning("[调试] 通关数据已改动，但本地存档保存失败");

        DebugCheats.Enable();
        Debug.Log("[调试] 当前轮回已通关，全部角色解锁，已发放全部法宝和最高品质装备；之后的关卡开启无敌、无冷却、无消耗、一击必杀");
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);//重新载入地图，刷新关卡入口与轮回提示
    }
}

//调试开关：开启后每次进入场景，都给 PlayerManager 创建的全部角色打开训练场同款的作弊开关。
public static class DebugCheats
{
    public static bool Active { get; private set; }
    public static bool OneHitKill => Active && !TrainingSession.Active;//玩家的所有攻击伤害都放大到足以秒杀
    private static PlayerManager bound;

    public static void Enable()
    {
        if (Active) return;
        Active = true;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()//关闭域重载时也保证每次进入 Play Mode 都从关闭状态开始
    {
        Active = false;
        bound = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (TrainingSession.Active) return;//训练场有自己的开关面板
        PlayerManager manager = Object.FindAnyObjectByType<PlayerManager>();
        if (manager == null) return;
        bound = manager;
        manager.CurrentPlayerChanged += OnPlayerChanged;//角色在关卡 Start 里才创建，创建完成和每次换人时补上开关
        Apply(manager);
    }

    private static void OnPlayerChanged(PlayerController player) => Apply(bound);

    private static void Apply(PlayerManager manager)
    {
        if (manager == null) return;
        manager.TrainingNoSwitchCooldown = true;
        manager.ClearSwitchCooldowns();
        foreach (PlayerController player in manager.Players)
        {
            if (player == null) continue;
            PlayerResources resources = player.GetComponent<PlayerResources>();
            if (resources != null) resources.TrainingInfiniteResources = true;//法力、气力无消耗
            PlayerCombat combat = player.GetComponent<PlayerCombat>();
            if (combat != null) combat.TrainingNoCooldown = true;//技能无冷却
            Health health = player.GetComponent<Health>();
            if (health != null) health.TrainingPreventHealthLoss = true;//无敌：受击照常，但不扣血
        }
    }
}
