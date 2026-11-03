using UnityEngine;

// Preserve inherited initialization and gravity. Run after the controller's motion
// step so a field's additive horizontal velocity cannot slide an occupied cast.
[DefaultExecutionOrder(100)]
public sealed class RedFurnaceLordMovement : EnemyMovement
{
    private RedFurnaceLordCombat combat;
    private void FixedUpdate()
    {
        if (combat == null) combat = GetComponent<RedFurnaceLordCombat>();
        if ((combat.IsCoreOpen || combat.CurrentSkill >= 0) && !combat.ControlInterrupted) Stop();
    }
}
