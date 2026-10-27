using UnityEngine;

[CreateAssetMenu(menuName = "Combat/Attack Effects/Poison")]
public class PoisonEffect : AttackEffect
{
    [SerializeField, Min(1)] private int stacks = 1;//一次成功命中增加的毒层，与沙僧两种施毒技能共用目标毒层
    [SerializeField, Min(0f)] private float duration = 5f;//施加时刷新整体持续时间
    [SerializeField, Min(0f)] private float damagePerStack = 1f;//每秒伤害按当前总层数线性增加

    public override void Apply(Health target, DamageInfo damage)//任意攻击箱可通过效果资源施毒，不需要为不同角色重复写中毒逻辑
    {
        if (target != null) target.ApplyPoison(damage.Source, duration, damagePerStack, stacks);
    }
}
