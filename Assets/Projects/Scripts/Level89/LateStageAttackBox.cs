using UnityEngine;

public sealed class LateStageAttackBox : EnemyAttackBox
{
    [SerializeField] private int index;
    protected override void TryAttack(Collider2D other)=>GetComponentInParent<LateStageCombat>()?.BoxContact(index,other);
}
