using UnityEngine;
using System.Collections.Generic;
public class PlayerAttackBox :AttackBox
{
    private PlayerCombat PC;
    protected override float DamageMultiplier => PC != null ? PC.AttackMultiplier : 1f;//唐僧无双等增益不改写基础Attack

    private void Awake() => PC = GetComponentInParent<PlayerCombat>();
    protected override void TryAttack(Collider2D collider)
    {
        IDamage enemy = collider.GetComponentInParent<IDamage>();
        if (enemy == null) return;
        if (enemy is Health enemyHealth && enemyHealth.Dead) return;//死亡动画中的敌人不提供无双能量
        if (!hitEnemies.Add(enemy)) return;
        if (PC != null)
            PC.UpdateBoost();
        enemy.TakeDamage(CreateDamageInfo(PC != null ? PC.transform : transform.root));
    }
}
