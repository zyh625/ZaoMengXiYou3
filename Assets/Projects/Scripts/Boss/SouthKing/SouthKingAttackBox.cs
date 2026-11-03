using UnityEngine;

//共用EnemyAttackBox去重和命中通知，额外补采样被渲染跨过的短攻击窗口。
public class SouthKingAttackBox : EnemyAttackBox
{
    private BoxCollider2D attackCollider;
    public void Sample(PlayerController player)
    {
        if (!isActiveAndEnabled || player == null || !player.CanReceiveHit) return;
        if (attackCollider == null) attackCollider = GetComponent<BoxCollider2D>();
        Collider2D target = player.GetComponent<Collider2D>();
        if (attackCollider == null || target == null || !target.enabled || !target.gameObject.activeInHierarchy) return;
        Vector3 center = attackCollider.transform.TransformPoint(attackCollider.offset);
        Vector3 scale = attackCollider.transform.lossyScale;
        Bounds area = new(center, new Vector3(Mathf.Abs(scale.x) * attackCollider.size.x,
            Mathf.Abs(scale.y) * attackCollider.size.y, 1f));//刚开启时不依赖下个Physics步才取得正确范围。
        if (area.Intersects(target.bounds)) TryAttack(target);
    }
    public override DamageInfo CreateDamageInfo(Transform source)
    {
        DamageInfo hit = base.CreateDamageInfo(source);
        return new DamageInfo(hit.Source, hit.Amount, hit.Knockback, hit.Launch, hit.Effects, kind: DamageKind.NormalAttack);
    }
}
