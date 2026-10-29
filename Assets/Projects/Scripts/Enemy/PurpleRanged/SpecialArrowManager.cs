using UnityEngine;

public class SpecialArrowManager : NormalArrowManager
{
    [SerializeField, Min(1)] private int poisonStacks = 3;//每次有效命中直接增加三层
    [SerializeField, Min(0.01f)] private float poisonDuration = 5f;//刷新为同一套毒素持续时间，换人不清除
    [SerializeField, Min(0f)] private float poisonDamagePerStack = 1f;//每层每秒伤害，层数线性叠加
    protected override void OnHit(Health health)
    {
        base.OnHit(health);
        if (health != null && !health.Dead && !health.IsInvincible)
            health.ApplyPoison(DamageSource, poisonDuration, poisonDamagePerStack, Mathf.Max(1, poisonStacks));
    }
}
