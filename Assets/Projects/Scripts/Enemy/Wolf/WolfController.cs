using UnityEngine;

public class WolfController : EnemyController
{
    private WolfMovement WM;
    protected override void Awake()
    {
        base.Awake();
        WM = GetComponent<WolfMovement>(); 
    }
    protected override void CheckSpecial()=> WM.CheckAttackTwoMove(SM.CurFrame);
    protected override void SpecialAttackSet(float dis) => 
        WM.AttackFaceRight = dis > 0f;
}