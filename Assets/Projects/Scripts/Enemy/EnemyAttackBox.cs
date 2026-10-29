using UnityEngine;

public class EnemyAttackBox : AttackBox
{
    private EnemyCombat EC;
    private void Awake() => EC = GetComponentInParent<EnemyCombat>();
    protected override float DamageMultiplier
    {
        get
        {
            if (EC == null) EC = GetComponentInParent<EnemyCombat>();//首次打开之前也允许查询真实伤害，不依赖攻击箱Awake时机
            return EC != null ? EC.DamageMultiplier : 1f;
        }
    }
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
