using System;
using UnityEngine;

public class TigerController : MonoBehaviour
{

    [SerializeField]
    private SpriteData Idle, Walk, AttackOne, AttackTwo,
        Attacked, Die;
    private SpriteManager SM;
    private Health TH;
    private EnemyMovement TM;
    [SerializeField] private EnemyState state;
    [SerializeField] private float stopDistance;
    
    private bool boost = false;
    private bool slow = false;
    void Awake()
    {
        SM=GetComponentInChildren<SpriteManager>();
        TH = GetComponent<Health>();
        TM = GetComponent<EnemyMovement>();
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
            TM.Stop();
            SetState(EnemyState.Die);
            return;
        }
 
        if(!boost&&TH.Attacked)
        {
            TH.AttackedEnd();
            TM.AttackedMove(!TH.AttackedFaceRight, TH.AttackedUp);
            SetState(EnemyState.Attacked);
            return;
        }
        if (AttackedProcessing()) return;
        TM.CheckIsGround();
        if (TM.AirState() != 0)//空中什么也做不了
        {
            SetState(EnemyState.Idle);
            return;
        }
        float dis = TH.DeltaX;
        //攻击
        if (Mathf.Abs(dis) <= stopDistance)//靠近玩家停止
        {
            TM.Stop();
            SetState(EnemyState.Idle);
            return;
        }
        SetState(EnemyState.Walk);
        SM.ChangeSpriteFace(dis > 0f);
        TM.Move(dis > 0, slow);
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
}
