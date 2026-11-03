using UnityEngine;
using UnityEngine.UI;

public class TigerController : EnemyController
{
    [SerializeField] private SpriteData AttackOne, AttackTwo;
    private TigerMovement TM;
    protected override void Awake()
    {
        base.Awake();
        TM = GetComponent<TigerMovement>();
    }
    protected override void SpecialAttackOver()//突刺攻击结束
    {
        TM.AttackTwoEnd();
        attackedBox.gameObject.SetActive(true);
    }
    protected override void CheckSpecialAttack()//虎怪突刺阶段获得无敌
    {
        int curState = TM.CheckAttackTwoMove(SM.CurFrame);
        if (curState == 0 || curState == 3) return;
        if (curState == 1)
            attackedBox.gameObject.SetActive(false);
        else if (curState == 2)
            attackedBox.gameObject.SetActive(true);
    }
    protected override void SpecialAttackSet(float dis)=>
        TM.AttackFaceRight = dis > 0f;//突刺方向确定，中途不变方向
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
        SM.ChangeSpriteFace(!r);//默认朝左
        EC.ChangeCheckBox(r);
    }
}
