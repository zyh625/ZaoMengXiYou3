using System.Collections.Generic;
using UnityEngine;

public class AttackBox : MonoBehaviour
{
    [SerializeField] private float Attack;
    [SerializeField] protected bool knockback = true;//是否施加水平击退，与击飞独立设置
    [SerializeField] protected bool launch;//是否施加竖直击飞，不再使用负伤害表示
    [SerializeField] protected AttackEffect[] effects;//可组合的额外命中效果，玩家攻击箱可沿用这些效果生成暴击伤害
    protected float curAttack;
    protected readonly HashSet<IDamage> hitEnemies = new();
    protected virtual float DamageMultiplier => 1f;//角色自己的攻击增益在真正命中时计算
    protected virtual void OnEnable()
    {
        curAttack = Mathf.Max(0f, Attack);
        hitEnemies.Clear();
    }
    protected void OnTriggerStay2D(Collider2D collider) => TryAttack(collider);
    protected void OnTriggerEnter2D(Collider2D collider) => TryAttack(collider);
    protected virtual void TryAttack(Collider2D collider)
    {

    }
    public void UpdateAttack() => hitEnemies.Clear();
    public virtual DamageInfo CreateDamageInfo(Transform source) =>
        new DamageInfo(source, curAttack * DamageMultiplier, knockback, launch, effects);
    public void ChangeAttack(float ratio) => curAttack *= Mathf.Max(0f, ratio);
    public void ReturnBegin() => curAttack = Mathf.Max(0f, Attack);
}
