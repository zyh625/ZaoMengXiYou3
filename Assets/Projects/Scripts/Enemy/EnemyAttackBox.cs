using UnityEngine;

public class EnemyAttackBox : AttackBox
{
    private EnemyCombat EC;
    private void Awake() => EC = GetComponentInParent<EnemyCombat>();
    protected override float DamageMultiplier => EC != null ? EC.DamageMultiplier : 1f;//动态读取Boss等攻击来源的额外倍率
    protected override void TryAttack(Collider2D collider)
    {
        IDamage player = collider.GetComponentInParent<IDamage>();
        if (player == null) return;
        if (!hitEnemies.Add(player)) return;
        if (EC != null)
            EC.UpdateAngry();
        Health health = collider.GetComponentInParent<Health>();//命中是否成功以实际扣血为准，不把接触无敌目标算作成功
        float previousBlood = health != null ? health.CurBlood : 0f;
        player.TakeDamage(CreateDamageInfo(EC != null ? EC.transform : transform.root));
        if (health != null && health.CurBlood < previousBlood)
            EC?.ReportAttackHit(this);
    }
}
