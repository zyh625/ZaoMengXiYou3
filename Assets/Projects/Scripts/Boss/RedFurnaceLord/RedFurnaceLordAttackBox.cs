using UnityEngine;

// EnemyAttackBox's trigger callbacks dispatch to this override. One owner handles both
// immediate overlap sampling and physics callbacks, so a target cannot be hit twice.
public sealed class RedFurnaceLordAttackBox : EnemyAttackBox
{
    [SerializeField] private int skill;
    private RedFurnaceLordCombat owner;
    protected override void TryAttack(Collider2D other)
    {
        if (owner == null) owner = GetComponentInParent<RedFurnaceLordCombat>();
        owner.TryMeleeHit(skill, other);
    }
}
