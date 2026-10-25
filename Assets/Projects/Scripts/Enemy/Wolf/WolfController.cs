using UnityEngine;

public class WolfController : EnemyController
{
    [SerializeField] private SpriteData AttackOne, AttackTwo;
    private WolfMovement WM;
    protected override void Awake()
    {
        base.Awake();
        WM = GetComponent<WolfMovement>(); 
    }
    protected override void CheckSpecialAttack()=> WM.CheckAttackTwoMove(SM.CurFrame);
    protected override void SpecialAttackSet(float dis) => 
        WM.AttackFaceRight = dis > 0f;
    protected override void HandleAttack(int attack)
    {
        switch (attack)
        {
            case 0:SM.ChangeState(AttackOne);break;
            case 1:SM.ChangeState(AttackTwo);break;
        }
    }
    public void ChangeFace(bool r)
    {
        SM.ChangeSpriteFace(!r);//默认朝右
        EC.ChangeCheckBox(r);
    }
}