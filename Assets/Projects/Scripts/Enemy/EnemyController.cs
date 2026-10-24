using UnityEngine;

public class EnemyController : MonoBehaviour
{
    [SerializeField]
    private SpriteData Idle, Walk, AttackOne, AttackTwo,
        Attacked, Die;
    protected SpriteManager SM;
    private EnemyMovement EM;
    private Health TH;
    private EnemyCombat TC;
    [SerializeField] private EnemyState state;
    [SerializeField] private float stopDistance;
    [SerializeField] protected BoxCollider2D attackedBox;
    [SerializeField] private int specialAttack;
    public float enemyHalf => GetComponent<BoxCollider2D>().bounds.size.x / 2;
    protected virtual void Awake()
    {
        SM = GetComponentInChildren<SpriteManager>(true);
        TH = GetComponent<Health>();
        EM = GetComponent<EnemyMovement>();
        TC = GetComponent<EnemyCombat>();
    }
    void OnEnable()
    {
        SetState(EnemyState.Idle);
        SM.ChangeState(Idle);
    }
    protected virtual void SpecialAttackOver() { }
    protected virtual void CheckSpecial() { }
    protected virtual void SpecialAttackSet(float dis) { }
    private void FixedUpdate()
    {
        if (DieProcessing()) return;
        if (TH.Dead)
        {
            EM.Stop(); AttackOver();
            SetState(EnemyState.Die);
            return;
        }
        if (TC.AngryRequest)
            ObtainAngry();
        if (TC.Angry && TC.AngryEnd)
            LoseAngry();
        if (!TH.Boost && TH.Attacked)
        {
            TH.AttackedEnd(); AttackOver();
            TC.UpdateAngry();
            EM.AttackedMove(TH.AttackedRight, TH.AttackedUp);
            SetState(EnemyState.Attacked);
            return;
        }
        if (AttackedProcessing()) return;
        EM.CheckIsGround();
        if (EM.AirState() != 0)//空中什么也做不了
        {
            AttackOver();
            SetState(EnemyState.Idle);
            return;
        }
        if (AttackProcessing())
        {
            if (TC.AttackOpen(SM.CurFrame)) TC.AttackBegin();
            if (TC.AttackShut(SM.CurFrame)) TC.AttackEnd();
            if (TC.Attack == specialAttack) CheckSpecial();
            return;
        }
        float dis = EM.DeltaX;
        TC.CheckAttack();
        if (TC.AttackReady())
        {
            TC.HasAtatck(dis > 0f); EM.Stop(); SpecialAttackSet(dis);
            SetState(TC.AttackRequest[0] ? EnemyState.AttackOne : EnemyState.AttackTwo);
            return;
        }
        SM.ChangeSpriteFace(dis > 0f);
        TC.ChangeCheckBox(dis > 0f);
        if (Mathf.Abs(dis) <= stopDistance)//靠近玩家停止
        {
            EM.Stop();
            SetState(EnemyState.Idle);
            return;
        }
        SetState(EnemyState.Walk);
        EM.Move(TH.Slow);
    }
    private void SetState(EnemyState next)
    {
        if (next == state && (state != EnemyState.Attacked)) return;
        state = next;
        switch (state)
        {
            case EnemyState.Idle: SM.ChangeState(Idle); break;
            case EnemyState.Walk: SM.ChangeState(Walk); break;
            case EnemyState.AttackOne: SM.ChangeState(AttackOne); break;
            case EnemyState.AttackTwo: SM.ChangeState(AttackTwo); break;
            case EnemyState.Attacked: SM.ChangeState(Attacked); break;
            case EnemyState.Die: SM.ChangeState(Die); break;
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
        if (TH.Boost || state != EnemyState.Attacked) return false;
        if (SM.IsFinished)
        {
            EM.Stop();
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
        SpecialAttackOver();
        attackedBox.gameObject.SetActive(true);
    }

    public void ChangeFace(bool r)
    {
        SM.ChangeSpriteFace(!r);//默认朝左
        TC.ChangeCheckBox(r);
    }
    private void ObtainAngry()
    {
        TC.AngryRequest = false;
        TC.Boost();
        SM.SetRed();
        SM.SetAngry(true);
        EM.Boost();
    }
    private void LoseAngry()
    {
        TC.BoostOver();
        SM.SetAngry(false);
        EM.ReturnSpeed();
    }
}
