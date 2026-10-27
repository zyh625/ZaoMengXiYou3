using UnityEngine;

//一次命中的完整数据；增加新的效果时只扩展AttackEffect，不再增加TakeDamage的参数。
public readonly struct DamageInfo
{
    public Transform Source { get; }
    public float Amount { get; }
    public bool Knockback { get; }
    public bool Launch { get; }
    public AttackEffect[] Effects { get; }
    public bool IsReflected { get; }
    public bool IsPeriodic { get; }//持续扣血只结算生命值，不再次触发受击、反伤和一次性易伤

    public DamageInfo(Transform source, float amount, bool knockback = true, bool launch = false,
        AttackEffect[] effects = null, bool isReflected = false, bool isPeriodic = false)
    {
        Source = source;
        Amount = Mathf.Max(0f, amount);
        Knockback = knockback;
        Launch = launch;
        Effects = effects;
        IsReflected = isReflected;
        IsPeriodic = isPeriodic;
    }
}
