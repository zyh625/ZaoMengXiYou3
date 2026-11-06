using UnityEngine;

//把新 PlayerAttackBox 的 IDamage 命中转发给旧鸟怪/Boss 的 IDamageable 接口。
//只用于第一关旧预制体，不改变新敌人的 Health 流程。
public sealed class LegacyDamageAdapter : MonoBehaviour, IDamage
{
    private IDamageable target;
    private void Awake() => Resolve();
    private void Resolve()
    {
        target = GetComponent<IDamageable>();
        if (target == null) target = GetComponentInChildren<IDamageable>(true);
    }
    public void TakeDamage(DamageInfo info)
    {
        if (target == null) Resolve();
        if (target != null) target.TakeDamage(info.Amount);
    }
    public void TakeDamage(Transform source, float amount)
    {
        if (target == null) Resolve();
        if (target != null) target.TakeDamage(amount);
    }
}
