using UnityEngine;

//把后续的中毒、减速等命中效果做成独立资源，攻击箱可以组合引用多个效果。
public abstract class AttackEffect : ScriptableObject
{
    public abstract void Apply(Health target, DamageInfo damage);
}
