using UnityEngine;

//本轮战斗统计只保留在内存，不进入SaveData；角色换人和持续伤害都按真实角色来源记账。
public static class BattleRun
{
    private sealed class Contribution
    {
        public double damageDealt, damageTaken, usedTime;//使用实际生命损失，避免过量伤害或无敌命中放大贡献
    }
    private static readonly Contribution[] contributions = new Contribution[4];
    private static readonly Health[] playerHealths = new Health[4];//四人生命快照引用，未上场角色视为满血
    private static Health boss;
    private static int waveIndex, waveCount, waveTotal, waveKills;
    private static int activePlayer = -1;
    private static float startedAt, lastUsageAt;
    private static bool bossEntered, bossDefeated;
    public static bool IsRunning { get; private set; }//金币必须在此值仍为true时完成吸附才可入账
    public static BattleData LastResult { get; private set; }//跨结算场景保留一次完整结果，不创建磁盘字段
    public static BattleData CurrentResult { get; private set; }

    public static void Begin(int level, PlayerManager manager)
    {
        boss = null;
        bossEntered = bossDefeated = false;
        waveIndex = waveCount = waveTotal = waveKills = 0;
        activePlayer = -1;
        startedAt = lastUsageAt = Time.time;
        LastResult = null;
        CurrentResult = new BattleData { curLevel = Mathf.Max(1, level) };
        for (int i = 0; i < contributions.Length; i++)
        {
            contributions[i] = new Contribution();
            playerHealths[i] = null;
        }
        if (manager != null)
            foreach (PlayerController player in manager.Players)
            {
                if (player == null) continue;
                PlayerResources resources = player.GetComponent<PlayerResources>();
                int id = resources != null ? (int)resources.Id : -1;
                if (id >= 0 && id < playerHealths.Length) playerHealths[id] = player.GetComponent<Health>();
            }
        IsRunning = true;
        SetCurrentPlayer(manager != null ? manager.CurrentPlayer : null);
        if (GameSession.instance != null) GameSession.instance.battle = CurrentResult;
    }

    public static void SetCurrentPlayer(PlayerController player)//切换时先结清上一人的使用时长，不靠对象OnDisable猜测
    {
        if (!IsRunning) return;
        AccumulateUsage();
        PlayerResources resources = player != null ? player.GetComponent<PlayerResources>() : null;
        activePlayer = resources != null ? (int)resources.Id : -1;
    }

    private static void AccumulateUsage()
    {
        float now = Time.time;
        if (activePlayer >= 0 && activePlayer < contributions.Length)
            contributions[activePlayer].usedTime += Mathf.Max(0f, now - lastUsageAt);
        lastUsageAt = now;
    }

    public static void RecordDamage(Health victim, DamageInfo damage, float actualDamage)
    {
        if (!IsRunning || victim == null || actualDamage <= 0f) return;
        PlayerResources defender = victim.GetComponent<PlayerResources>();
        if (defender != null)
        {
            int id = (int)defender.Id;
            if (id >= 0 && id < contributions.Length) contributions[id].damageTaken += actualDamage;
            return;//玩家之间的误命中不计为对敌输出
        }
        if (victim.GetComponent<EnemyMovement>() == null || damage.Source == null) return;
        PlayerResources attacker = damage.Source.GetComponentInParent<PlayerResources>(true);//换出的沙僧毒素仍归沙僧
        if (attacker == null) return;
        int sourceId = (int)attacker.Id;
        if (sourceId >= 0 && sourceId < contributions.Length) contributions[sourceId].damageDealt += actualDamage;
    }

    public static void SetWave(int index, int totalWaves, int totalThisWave)
    {
        if (!IsRunning) return;
        if (waveIndex != index) waveKills = 0;
        waveIndex = Mathf.Max(0, index);
        waveCount = Mathf.Max(0, totalWaves);
        waveTotal = Mathf.Max(0, totalThisWave);
    }

    public static void RecordEnemyKill(bool isBoss)
    {
        if (!IsRunning) return;
        if (isBoss) bossDefeated = true;
        else waveKills = Mathf.Min(waveTotal, waveKills + 1);
    }

    public static void SetBoss(Health value)//只在Boss真正现身进入战斗时调用，出场等待仍属于小怪进度上限70%
    {
        if (!IsRunning) return;
        boss = value;
        bossEntered = true;
    }

    public static BattleData Finish(bool victory)
    {
        if (!IsRunning) return LastResult ?? CurrentResult;
        AccumulateUsage();
        IsRunning = false;//先关统计和拾取资格，再计算并切场景
        CurrentResult.costTime = Mathf.Max(0, Mathf.FloorToInt(Time.time - startedAt));
        CurrentResult.playerState = AverageHealth();
        CurrentResult.mvp = SelectMVP();
        CurrentResult.progress = victory ? 100 : CalculateProgress();
        CurrentResult.victory = victory;
        LastResult = CurrentResult;
        if (GameSession.instance != null) GameSession.instance.battle = LastResult;
        return LastResult;
    }

    private static int AverageHealth()
    {
        float sum = 0f;
        for (int i = 0; i < playerHealths.Length; i++)
        {
            Health health = playerHealths[i];
            if (health == null || health.LifeVersion == 0) sum += 1f;//备用角色还没OnEnable，不能用序列化的0血量当成已死
            else if (health.BeginBlood > 0f) sum += Mathf.Clamp01(health.CurBlood / health.BeginBlood);
        }
        return Mathf.Clamp(Mathf.FloorToInt(sum / playerHealths.Length * 100f), 0, 100);
    }

    private static int SelectMVP()
    {
        double dealt = 0, taken = 0, used = 0;
        foreach (Contribution contribution in contributions)
        {
            dealt += contribution.damageDealt;
            taken += contribution.damageTaken;
            used += contribution.usedTime;
        }
        int best = 0;
        double bestScore = -1;
        for (int i = 0; i < contributions.Length; i++)
        {
            Contribution contribution = contributions[i];
            double score = (dealt > 0 ? contribution.damageDealt / dealt * 0.3 : 0)
                + (taken > 0 ? contribution.damageTaken / taken * 0.3 : 0)
                + (used > 0 ? contribution.usedTime / used * 0.4 : 0);
            if (score > bestScore + 0.0000001
                || (System.Math.Abs(score - bestScore) <= 0.0000001 && contribution.usedTime > contributions[best].usedTime))
            {
                bestScore = score;
                best = i;//完全同分同时间时保留更小PlayerId，避免结果随机跳变
            }
        }
        return best;
    }

    private static int CalculateProgress()
    {
        if (bossDefeated) return 100;
        if (bossEntered)
        {
            float ratio = boss != null && boss.BeginBlood > 0f ? Mathf.Clamp01(boss.CurBlood / boss.BeginBlood) : 1f;
            return Mathf.Clamp(Mathf.FloorToInt(70f + 30f * (1f - ratio)), 70, 100);
        }
        if (waveCount <= 0) return 0;
        float fraction = waveTotal > 0 ? Mathf.Clamp01((float)waveKills / waveTotal) : 1f;
        return Mathf.Clamp(Mathf.FloorToInt(70f / waveCount * (Mathf.Min(waveIndex, waveCount) + fraction)), 0, 70);
    }

    public static int GetSceneLevel(string sceneName)//编辑器直接运行关卡同样使用实际场景编号
    {
        if (!string.IsNullOrEmpty(sceneName) && sceneName.StartsWith("Level") && sceneName.EndsWith("Scene")
            && sceneName.Length > 10 && int.TryParse(sceneName.Substring(5, sceneName.Length - 10), out int level))
            return Mathf.Max(1, level);
        return 1;
    }

    public static void Cancel() => ResetSession();//切换存档只丢弃旧轮统计，不让旧MVP、耗时或运行标记跨档串用

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()//关闭Domain Reload后重新进入播放也不带入上轮结果
    {
        IsRunning = false;
        LastResult = CurrentResult = null;
        boss = null;
    }
}
