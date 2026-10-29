using UnityEngine;

public enum DamageKind { Unspecified, NormalAttack, Skill }//命中类别独立于伤害数值，虚化只过滤普攻而不影响技能与持续状态

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
    public DamageKind Kind { get; }

    public DamageInfo(Transform source, float amount, bool knockback = true, bool launch = false,
        AttackEffect[] effects = null, bool isReflected = false, bool isPeriodic = false, DamageKind kind = DamageKind.Unspecified)
    {
        Source = source;
        Amount = Mathf.Max(0f, amount);
        Knockback = knockback;
        Launch = launch;
        Effects = effects;
        IsReflected = isReflected;
        IsPeriodic = isPeriodic;
        Kind = kind;
    }
}
