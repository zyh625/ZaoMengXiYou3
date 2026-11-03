using UnityEngine;

[CreateAssetMenu(menuName = "Combat/Attack Effects/Frozen")]
public class FrozenEffect : AttackEffect
{
    [SerializeField, Min(0f)] private float duration = 2f;//命中后的完全冰冻时间，随后按目标配置开始融化

    public override void Apply(Health target, DamageInfo damage)//任意玩家或敌人的攻击箱都可以挂同一类冰冻效果资源
    {
        if (target != null && target.Status != null) target.Status.ApplyFreeze(this, duration);
    }
}
