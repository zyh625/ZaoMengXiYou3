using UnityEngine;

//三种普攻共用概率冰冻；实际命中时查询寒霜，不在起手时缓存旧概率。
public class SouthKingFreezeEffect : AttackEffect
{
    public override void Apply(Health target, DamageInfo damage)
    {
        SouthKingCombat source = damage.Source != null ? damage.Source.GetComponentInParent<SouthKingCombat>() : null;
        if (target != null && source != null && Random.value < source.NormalFreezeChance)
            target.Status?.ApplyFreeze(this, source.FreezeDuration);
    }
}
