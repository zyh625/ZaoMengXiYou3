using UnityEngine;
using System.Collections.Generic;
public class PlayerAttackBox :AttackBox
{
    private PlayerCombat PC;

    private void Awake() => PC = GetComponentInParent<PlayerCombat>();
    protected override void TryAttack(Collider2D collider)
    {
        IDamage enemy = collider.GetComponentInParent<IDamage>();
        if (enemy == null) return;
        if (!hitEnemies.Add(enemy)) return;
        if (PC != null)
            PC.UpdateBoost();
        enemy.TakeDamage(PC.GetComponent<Transform>(), curAttack);
    }
}
