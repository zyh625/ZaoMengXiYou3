using UnityEngine;

public class PurpelMovement : EnemyMovement
{
    public void PrepareParry()//驱散已归还重力和位置锁；终止触发前残留的击退或击飞速度，随后正常受重力影响
    {
        if (body != null) body.linearVelocity = Vector2.zero;
    }
}
