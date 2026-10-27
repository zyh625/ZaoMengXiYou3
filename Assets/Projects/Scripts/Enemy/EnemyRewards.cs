using UnityEngine;

[RequireComponent(typeof(Health))]
public class EnemyRewards : MonoBehaviour
{
    [SerializeField, Min(0f)] private float experienceReward = 1f;//每次真正击杀该敌人获得的经验，可在预制体分别调整
    private Health health;//奖励只由血量死亡事件触发，不把普通停用或回收算成击杀
    private PlayerManager playerManager;//由关卡注入，奖励归死亡时正在使用的角色
    private bool rewarded;//同一次生命最多结算一次，重新出池后才允许再次奖励
    [SerializeField] private bool isBoss;//首杀金币与波次击杀统计区分Boss和普通池成员
    private GoldManager goldManager;//每次死亡只产生一个承载完整金额的金币对象

    private void Awake() => health = GetComponent<Health>();

    private void OnEnable()
    {
        rewarded = false;
        health.Died += OnDied;
    }

    private void OnDisable() => health.Died -= OnDied;

    public void SetPlayerManager(PlayerManager players)
    {
        playerManager = players;
        goldManager = players != null ? players.GetComponent<GoldManager>() : null;
    }

    public void SetBoss(bool value) => isBoss = value;//由关卡创建Boss时指定，不依赖敌人脚本类名或预制体名称

    private void OnDied(DamageInfo damage)
    {
        if (rewarded || !BattleRun.IsRunning) return;//死亡后的同帧残留毒伤不能在结算已冻结后继续追加经验或首杀奖励
        rewarded = true;
        BattleRun.RecordEnemyKill(isBoss);//死亡即统计击杀，不等死亡动画结束或对象池停用
        if (goldManager != null) goldManager.Drop(transform, isBoss);
        PlayerController player = playerManager == null ? null : playerManager.CurrentPlayer;
        if (player == null) return;
        PlayerResources resources = player.GetComponent<PlayerResources>();//切换不会转移或清空其他角色已有的经验
        if (resources != null) resources.AddExperience(experienceReward);
    }
}
