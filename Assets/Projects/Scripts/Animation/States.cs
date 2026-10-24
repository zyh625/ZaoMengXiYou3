using UnityEngine;

public enum PlayerState
{
    Idle,
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
    RiseAttack
}
public enum EnemyState
{
    Idle, Walk, Die, AttackOne,AttackTwo,Attacked
}
public enum TitanState
{
    Idle,Walk,Lunge,Boost,Attack,Attacked,Die,Tremor
}