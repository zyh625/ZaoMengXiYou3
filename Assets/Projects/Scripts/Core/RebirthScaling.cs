using System;
using UnityEngine;
using UnityEngine.SceneManagement;

//轮回次数保留在存档中；只有数值计算封顶，绝不写回预制体或基础配置。
public static class RebirthScaling
{
    private static float Rule(string key,float fallback)=>BalanceRuntime.Load("Common/Rebirth")?.Get(key,fallback)??fallback;
    public static int Cap => Mathf.Max(0,Mathf.RoundToInt(Rule("Cap",10)));
    public readonly struct Multipliers
    {
        public readonly int N;
        public Multipliers(int rebirth) => N = Mathf.Clamp(rebirth, 0, Cap);
        public float HP => 1f + Rule("HPPerRound",.35f) * N;
        public float Damage => 1f + Rule("DamagePerRound",.15f) * N;
        public float Gold => 1f + Rule("GoldPerRound",.25f) * N;
        public float Experience => 1f + Rule("ExperiencePerRound",.20f) * N;
        public float Quantity => 1f + Rule("QuantityPerRound",.08f) * N;
        public float AttackCooldown => Mathf.Max(Rule("MinimumAttackCooldown",.75f),1f-Rule("AttackCooldownPerRound",.03f)*N);
        public float Movement => 1f + Rule("MovementPerRound",.02f)*N;
        public float SpawnDelay => Mathf.Max(Rule("MinimumSpawnDelay",.70f),1f-Rule("SpawnDelayPerRound",.03f)*N);
        public int ExtraAlive => Mathf.Clamp(Mathf.CeilToInt(Rule("AlivePerRound",.1f)*N),0,Mathf.RoundToInt(Rule("MaximumExtraAlive",1)));
        private long QuantityStep=>Mathf.RoundToInt(Rule("QuantityPerRound",.08f)*10000);
        private long GoldStep=>Mathf.RoundToInt(Rule("GoldPerRound",.25f)*10000);

        public int EnemyCount(int baseline)//数量向上取整，用整数避免浮点误差多生一只。
            => (int)Math.Min(int.MaxValue, (Math.Max(0L, baseline) * (10000L + QuantityStep * N) + 9999L) / 10000L);
        public int GoldAmount(int baseline)//金币只能是整数，最终金额四舍五入；不改变掉落概率。
            => (int)Math.Min(int.MaxValue, (Math.Max(0L, baseline) * (10000L + GoldStep * N) + 5000L) / 10000L);
        public void ScaleWaves(int[] leftBase, int[] rightBase, out int[] left, out int[] right)
        {
            left = leftBase != null ? (int[])leftBase.Clone() : null;
            right = rightBase != null ? (int[])rightBase.Clone() : null;
            if (N == 0 || left == null || right == null || left.Length != right.Length) return;
            int entries = left.Length * 2;
            int[] remainders = new int[entries]; long total = 0L, allocated = 0L;
            for (int i = 0; i < entries; i++)
            {
                int baseline = i % 2 == 0 ? leftBase[i / 2] : rightBase[i / 2];
                if (baseline < 0) return;//保留非法配置，让关卡原有检查报告问题。
                long numerator = (long)baseline * (10000 + QuantityStep * N);
                int count = (int)Math.Min(int.MaxValue, numerator / 10000L);
                if (i % 2 == 0) left[i / 2] = count; else right[i / 2] = count;
                total += baseline; allocated += count; remainders[i] = baseline == 0 ? -1 : (int)(numerator % 10000L);
            }
            long target = (total * (10000 + QuantityStep * N) + 9999L) / 10000L;
            //整关总数先向上取整，再用最大余数法分给各波左右侧，避免逐项ceil放大超出设计比例。
            for (long extra = target - allocated; extra > 0; extra--)
            {
                int best = -1;
                for (int i = 0; i < entries; i++) if (remainders[i] >= 0 && (best < 0 || remainders[i] > remainders[best])) best = i;
                if (best < 0) break;
                if (best % 2 == 0) left[best / 2]++; else right[best / 2]++;
                remainders[best] = -1;
            }
        }

        public int[] EnemyCounts(int[] baseline)
        {
            if (baseline == null) return null;
            int[] runtime = new int[baseline.Length];
            for (int i = 0; i < runtime.Length; i++) runtime[i] = baseline[i] < 0 ? baseline[i] : EnemyCount(baseline[i]);
            return runtime;
        }
    }

    public static Multipliers For(int rebirth) => new Multipliers(rebirth);
    public static Multipliers ForLevel(int level)
        => For(!TrainingSession.Active && level >= 1 && level <= 9 && GameSession.instance != null
            ? GameSession.instance.Rebirth : 0);

    public static Multipliers Current
    {
        get
        {
            //第9关现为轮回终点；九个正式关卡共用倍率，菜单和训练场保持基础值。
            int level = SceneManager.GetActiveScene().name switch
            {
                "Level1Scene" => 1, "Level2Scene" => 2, "Level3Scene" => 3,
                "Level4Scene" => 4, "Level5Scene" => 5, "Level6Scene" => 6,
                "Level7Scene" => 7, "Level8Scene" => 8, "Level9Scene" => 9, _ => 0
            };
            return ForLevel(level);
        }
    }
}
