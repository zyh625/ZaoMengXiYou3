using UnityEngine;

public class EnemyAttackBox : AttackBox
{
    private EnemyCombat EC;
    private void Awake() => EC = GetComponentInParent<EnemyCombat>();
    protected override void TryAttack(Collider2D collider)
    {
        IDamage player = collider.GetComponentInParent<IDamage>();
        if (player == null) return;
        if (!hitEnemies.Add(player)) return;
        if (EC != null)
            EC.UpdateAngry();
        player.TakeDamage(CreateDamageInfo(EC != null ? EC.transform : transform.root));
    }
}
