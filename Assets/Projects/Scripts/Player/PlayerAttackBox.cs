using UnityEngine;
using System.Collections.Generic;
public class PlayerAttackBox :AttackBox
{
    private PlayerCombat PC;
    private void Awake() => PC = GetComponentInParent<PlayerCombat>();
    public override DamageInfo CreateDamageInfo(Transform source)//继承原攻击箱开关和命中记录，伤害统一交给角色的倍率与暴击入口
        => PC != null ? PC.CreateAttackDamage(curAttack, knockback, launch, effects, source,
            PC.Skilled != 0 ? DamageKind.Skill : DamageKind.NormalAttack) : base.CreateDamageInfo(source);

    protected override void TryAttack(Collider2D collider)
    {
        IDamage enemy = collider.GetComponentInParent<IDamage>();
        if (enemy == null) return;
        if (enemy is Health enemyHealth && enemyHealth.Dead) return;//死亡动画中的敌人不提供无双能量
        if (enemy is Health target && target.IgnoresAttack(PC != null && PC.Skilled != 0 ? DamageKind.Skill : DamageKind.NormalAttack)) return;
        if (!hitEnemies.Add(enemy)) return;
        Health health = enemy as Health;//只按实际生命损失积累一次无双，不因暴击额外累计第二次
        float previousBlood = health != null ? health.CurBlood : 0f;
        enemy.TakeDamage(CreateDamageInfo(PC != null ? PC.transform : transform.root));
        if (PC != null && (health == null || health.CurBlood < previousBlood)) PC.UpdateBoost();
    }
}
