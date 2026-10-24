using UnityEngine;

public class TigerController : EnemyController
{
    private TigerMovement TM;
    protected override void Awake()
    {
        base.Awake();
        TM = GetComponent<TigerMovement>();
    }
    protected override void SpecialAttackOver() => TM.AttackTwoEnd();
    protected override void CheckSpecial()
    {
        int curState = TM.CheckAttackTwoMove(SM.CurFrame);
        if (curState == 0 || curState == 3) return;
        if (curState == 1)
            attackedBox.gameObject.SetActive(false);
        else if (curState == 2)
            attackedBox.gameObject.SetActive(true);
    }
    protected override void SpecialAttackSet(float dis)=> 
        TM.AttackFaceRight = dis > 0f;
}
