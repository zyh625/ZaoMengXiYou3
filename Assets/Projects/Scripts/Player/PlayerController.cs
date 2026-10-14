using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("动作帧素材")]
    [SerializeField] private SpriteData Idle;
    [SerializeField] private SpriteData Walk;
    [SerializeField] private SpriteData Run;
    [SerializeField] private SpriteData JumpOne;
    [SerializeField] private SpriteData JumpTwo;
    [SerializeField] private SpriteData AttackOne;
    [SerializeField] private SpriteData AttackTwo;
    [SerializeField] private SpriteData AttackThree;
    [SerializeField] private SpriteData SkillOne;
    [SerializeField] private SpriteData SkillTwo;
    [SerializeField] private SpriteData SkillThree;
    [SerializeField] private SpriteData Attacked;
    [SerializeField] private SpriteData Die;
    [SerializeField] private SpriteData ComboAttackOne;
    [SerializeField] private SpriteData ComboAttackTwo;
    [Header("引用其他组件")]
    [SerializeField] private SpriteManager SM;
    private PlayerState state = PlayerState.Idle;//当前状态,初始为待机
    public PlayerState State() => state;
    void Awake()
    {
        SetState(PlayerState.Idle);
    }

    private void SetState(PlayerState next)
    {
        if (state == next) return;
        state = next;
        switch (state)
        {
            case PlayerState.Idle:SM.ChangeState(Idle);break;
            case PlayerState.Walk:SM.ChangeState(Walk);break;
            case PlayerState.Run:SM.ChangeState(Run);break;
            case PlayerState.JumpOne:SM.ChangeState(JumpOne);break;
            case PlayerState.JumpTwo:SM.ChangeState(JumpTwo);break;
            case PlayerState.AttackOne:SM.ChangeState(AttackOne);break;
            case PlayerState.AttackTwo:SM.ChangeState(AttackTwo);break;
            case PlayerState.AttackThree:SM.ChangeState(AttackThree);break;
            case PlayerState.Attacked:SM.ChangeState(Attacked);break;
            case PlayerState.ComboAttackOne:SM.ChangeState(ComboAttackOne);break;
            case PlayerState.ComboAttackTwo:SM.ChangeState(ComboAttackTwo);break;
            case PlayerState.Die:SM.ChangeState(Die);break;
            case PlayerState.SkillOne:SM.ChangeState(SkillOne);break;
            case PlayerState.SkillTwo:SM.ChangeState(SkillTwo);break;
            case PlayerState.SkillThree:SM.ChangeState(SkillThree);break;
        }
    }

}
