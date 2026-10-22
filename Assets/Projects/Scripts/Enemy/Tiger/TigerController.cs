using System;
using UnityEngine;

public class TigerController : MonoBehaviour
{

    [SerializeField]
    private SpriteData Idle, Walk, AttackOne, AttackTwo,
        Attacked, Die;
    private SpriteManager SM;
    private Health TH;
    private TigerMovement TM;
    private EnemyCombat TC;
    [SerializeField] private EnemyState state;
    [SerializeField] private float stopDistance;
    [SerializeField] private BoxCollider2D attackedBox;
    
    private bool boost = false;
    private bool slow = false;
    void Awake()
    {
        SM=GetComponentInChildren<SpriteManager>();
        TH = GetComponent<Health>();
        TM = GetComponent<TigerMovement>();
        TC = GetComponent<EnemyCombat>();
    }
    void Start()
    {
        SetState(EnemyState.Idle);
        SM.ChangeState(Idle);
    }
    private void FixedUpdate()
    {
        if (DieProcessing()) return;
        if (TH.Dead)
        {
            TM.Stop();AttackOver();
            SetState(EnemyState.Die);
            return;
        }
 
        if(!boost&&TH.Attacked)
        {
            TH.AttackedEnd();AttackOver();
            TM.AttackedMove(TH.AttackedRight, TH.AttackedUp);
            SetState(EnemyState.Attacked);
            return;
        }
        if (AttackedProcessing()) return;
        TM.CheckIsGround();
        if (TM.AirState() != 0)//空中什么也做不了
        {
            AttackOver();
            SetState(EnemyState.Idle);
            return;
        }
        if (AttackProcessing())
        {
            if (TC.AttackOpen(SM.CurFrame)) TC.AttackBegin();
            if (TC.AttackShut(SM.CurFrame)) TC.AttackEnd();
            if (TC.Attack == 1) CheckAttackTwoLunge();
            return;
        }
        TC.CheckAttack();
        float dis = TM.DeltaX;
        if (TC.AttackReady)
        {
            TC.HasAtatck(dis > 0f);TM.Stop();
            SetState(TC.AttackRequest[0] ? EnemyState.AttackOne : EnemyState.AttackTwo);
            return;
        }
        SM.ChangeSpriteFace(dis > 0f);
        TC.ChangeCheckBox(dis > 0f);
        if (Mathf.Abs(dis) <= stopDistance)//靠近玩家停止
        {
            TM.Stop();
            SetState(EnemyState.Idle);
            return;
        }
        SetState(EnemyState.Walk);
        TM.Move(slow);
    }
    private void SetState(EnemyState next)
    {
        if (next == state && (state != EnemyState.Attacked)) return;
        state = next;
        switch (state)
        {
            case EnemyState.Idle:SM.ChangeState(Idle);break;
            case EnemyState.Walk:SM.ChangeState(Walk); break;
            case EnemyState.AttackOne:SM.ChangeState(AttackOne);break;
            case EnemyState.AttackTwo:SM.ChangeState(AttackTwo);break;
            case EnemyState.Attacked:SM.ChangeState(Attacked);break;
            case EnemyState.Die:SM.ChangeState(Die);break;
        }
    }
    private bool DieProcessing()
    {
        if (state != EnemyState.Die) return false;
        if (SM.IsFinished) gameObject.SetActive(false);
        return true;
    }
    private bool AttackedProcessing()
    {
        if (boost || state != EnemyState.Attacked) return false;
        if (SM.IsFinished)
        {
            TM.Stop();
            return false;
        }
        return true;
    }
    private bool AttackProcessing()
    {
        if (TC.Attack == -1) return false;
        if (SM.IsFinished)
        {
            AttackOver();
            return false;
        }
        return true;
    }
    private void AttackOver()
    {
        TC.AttackEnd();
        TC.AttackOver();
        TM.AttackTwoEnd();
    }
    private void CheckAttackTwoLunge()
    {
        int curState = TM.CheckAttackTwoMove(SM.CurFrame);
        if (curState == 0 || curState == 3) return;
        if (curState == 1)
            attackedBox.gameObject.SetActive(false);
        else if (curState == 2)
            attackedBox.gameObject.SetActive(true);
    }

}
