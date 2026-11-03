using System;
using UnityEngine;
using UnityEngine.SceneManagement;

//轮回次数保留在存档中；只有数值计算封顶，绝不写回预制体或基础配置。
public static class RebirthScaling
{
    public const int Cap = 10;
    public readonly struct Multipliers
    {
        public readonly int N;
        public Multipliers(int rebirth) => N = Mathf.Clamp(rebirth, 0, Cap);
        public float HP => 1f + 0.35f * N;
        public float Damage => 1f + 0.15f * N;
        public float Gold => 1f + 0.25f * N;
        public float Experience => 1f + 0.20f * N;
        public float Quantity => 1f + 0.08f * N;
        public float AttackCooldown => Mathf.Max(0.75f, 1f - 0.03f * N);
        public float Movement => 1f + 0.02f * N;
        public float SpawnDelay => Mathf.Max(0.70f, 1f - 0.03f * N);
        public int ExtraAlive => N > 0 ? 1 : 0;//ceil(0.1N)，N在0..10间。

        public int EnemyCount(int baseline)//数量向上取整，用整数避免浮点误差多生一只。
            => (int)Math.Min(int.MaxValue, (Math.Max(0L, baseline) * (100L + 8L * N) + 99L) / 100L);
        public int GoldAmount(int baseline)//金币只能是整数，最终金额四舍五入；不改变掉落概率。
            => (int)Math.Min(int.MaxValue, (Math.Max(0L, baseline) * (100L + 25L * N) + 50L) / 100L);
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
                long numerator = (long)baseline * (100 + 8 * N);
                int count = (int)Math.Min(int.MaxValue, numerator / 100L);
                if (i % 2 == 0) left[i / 2] = count; else right[i / 2] = count;
                total += baseline; allocated += count; remainders[i] = baseline == 0 ? -1 : (int)(numerator % 100L);
            }
            long target = (total * (100 + 8 * N) + 99L) / 100L;
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
