using UnityEngine;

public class ShaController : PlayerController
{
    [SerializeField] private SpriteData JumpThree;//沙僧独立的第三段跳跃动画
    private PlayerMovement movement;//沿用基类跳跃计数和气力消耗
    private ShaCombat combat;//只把动作帧通知沙僧战斗组件，公共施法优先级继续使用基类

    protected override void Awake()
    {
        base.Awake();
        movement = GetComponent<PlayerMovement>();
        combat = GetComponent<ShaCombat>();
    }

    protected override void StartSkill()
    {
        base.StartSkill();
        combat.BeginSkill();
    }

    protected override bool CanStartSkill() => combat.CanStartSkill();

    protected override void SpecialSkill()
    {
        base.SpecialSkill();
        combat.CheckSkillEffect(SM.CurFrame, movement.FaceRight);//有效帧使用当下位置和朝向，之前被打断就不会生成
    }

    protected override void UpdateMovement()//沙僧施法时继续沿用普通移动，人物朝向与生成投射物的方向保持一致
    {
        base.UpdateMovement();
        if (state == PlayerState.Skill) SM.ChangeSpriteFace(movement.FaceRight);
    }

    protected override void HandleJump()
    {
        if (movement.HasJumped >= 3 && JumpThree != null && JumpThree.sprites != null && JumpThree.sprites.Length > 0)
            SM.ChangeState(JumpThree);
        else base.HandleJump();
    }
}
