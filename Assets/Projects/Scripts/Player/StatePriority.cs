using System.Collections.Generic;
using Unity.IO.LowLevel.Unsafe;
using UnityEngine;
public class StateRule
{
    public int priority;

}
public class StatePriority : MonoBehaviour
{
    public readonly Dictionary<PlayerState, StateRule> rules = new();
    [SerializeField]
    private int Idle,
    Walk,
    Run,
    JumpOne,
    JumpTwo,
    AttackOne,
    AttackTwo,
    AttackThree,
    SkillOne,
    SkillTwo,
    SkillThree,
    Attacked,
    Die,
    ComboAttackOne,
    ComboAttackTwo,
    Drop,
    Rise,
    Land,
    RiseAttack;
    private void Start()
    {
        rules[PlayerState.Idle] = new() { priority = Idle };
        rules[PlayerState.Walk] = new() { priority = Walk };
        rules[PlayerState.Run] = new() { priority = Run };
        rules[PlayerState.JumpOne] = new() { priority = JumpOne };
        rules[PlayerState.JumpTwo] = new() { priority = JumpTwo };
        rules[PlayerState.AttackOne] = new() { priority = AttackOne };
        rules[PlayerState.AttackTwo] = new() { priority = AttackTwo };
        rules[PlayerState.AttackThree] = new() { priority = AttackThree };
        rules[PlayerState.SkillOne] = new() { priority = SkillOne };
        rules[PlayerState.SkillTwo] = new() { priority = SkillTwo };
        rules[PlayerState.SkillThree] = new() { priority = SkillThree };
        rules[PlayerState.Attacked] = new() { priority = Attacked };
        rules[PlayerState.Die] = new() { priority = Die };
        rules[PlayerState.ComboAttackOne] = new() { priority = ComboAttackOne };
        rules[PlayerState.ComboAttackTwo] = new() { priority = ComboAttackTwo };
        rules[PlayerState.Drop] = new() { priority = Drop };
        rules[PlayerState.Rise] = new() { priority = Rise };
        rules[PlayerState.Land] = new() { priority = Land };
        rules[PlayerState.RiseAttack] = new() { priority = RiseAttack };

    }
}
