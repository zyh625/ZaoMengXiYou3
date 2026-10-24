using Unity.VisualScripting;
using UnityEngine;

public class TitanController : MonoBehaviour
{
    [SerializeField]
    private SpriteData Idle, Walk, Boost, Lunge, Attacked,
        Attack, Die, Tremor;
    [SerializeField] private float stopDistance, boostRatio, boostDuration;
    private TitanState state;
    private SpriteManager SM;
    private TitanMovement TM;
    private EnemyCombat TC;
    private Health TH;
    [SerializeField] private BoxCollider2D attackedBox;
    [SerializeField] private float[] bloodRatio;//血量降低到对应阈值使用强化，可被打断，短暂攻击增幅、减伤、霸体
    private int curBoostTime = 0;
    private float beginBoostTime = -1f;
    private bool skilled = false;
    private void Awake()
    {
        SM = GetComponentInChildren<SpriteManager>();
        TM = GetComponent<TitanMovement>();
        TC = GetComponent<EnemyCombat>();
        TH = GetComponent<Health>();
    }
    private void OnEnable()
    {
        curBoostTime = 0;
        state = TitanState.Idle;
        SM.ChangeState(Idle);
    }
    private void FixedUpdate()
    {
        if (DieProcessing()) return;
        if (TH.Dead)
        {
            AttackOver();
            SetState(TitanState.Die);
            return;
        }
        if (TC.AngryRequest && !skilled)//愤怒和减伤不能同时生效
            ObtainAngry();
        if (TC.Angry && TC.AngryEnd)
            LoseAngry();
        if (!TH.Boost&&state!=TitanState.Boost&&curBoostTime < bloodRatio.Length
            && TH.ratio <= bloodRatio[curBoostTime])
        {
            curBoostTime++;AttackOver();
            SetState(TitanState.Boost);
        }
        if (BoostProcessing()) return;
        if (Time.time - beginBoostTime >= boostDuration) LoseBoost();
        if (!TH.Boost && TH.Attacked)
        {
            TH.AttackedEnd(); AttackOver();
            TC.UpdateAngry();
            TM.AttackedMove(TH.AttackedRight, TH.AttackedUp);
            SetState(TitanState.Attacked);
            return;
        }
        if (AttackedProcessing()) return;
        TM.CheckIsGround();
        if (TM.AirState() != 0)//空中
        {
            SetState(TitanState.Idle);
            return;
        }
        if (AttackProcessing())
        {
            if (TC.AttackOpen(SM.CurFrame)) TC.AttackBegin();
            if (TC.AttackShut(SM.CurFrame)) TC.AttackEnd();
            if (TC.Attack == 1) CheckLunge();
            return;
        }
        float dis = TM.DeltaX;
        TC.CheckAttack();
        if (TC.AttackReady())
        {
            TC.HasAtatck(dis > 0f); TM.Stop(); TM.AttackFaceRight = dis > 0f;
            attackedBox.gameObject.SetActive(false);//攻击过程无敌
            SetState(TC.AttackRequest[0] ? TitanState.Attack : TC.AttackRequest[1]
                ? TitanState.Lunge : TitanState.Tremor);//劈砍，冲刺，震地
            return;
        }
        SM.ChangeSpriteFace(dis > 0f);
        TC.ChangeCheckBox(dis > 0f);
        if (Mathf.Abs(dis) <= stopDistance)//靠近玩家停止
        {
            TM.Stop();
            SetState(TitanState.Idle);
            return;
        }
        SetState(TitanState.Walk);
        TM.Move(TH.Slow);
    }

    private void SetState(TitanState next)
    {
        if (next == state && (state != TitanState.Attacked)) return;
        state = next;
        switch (state)
        {
            case TitanState.Idle: SM.ChangeState(Idle); break;
            case TitanState.Walk: SM.ChangeState(Walk); break;
            case TitanState.Boost: SM.ChangeState(Boost); break;
            case TitanState.Attack: SM.ChangeState(Attack); break;
            case TitanState.Attacked: SM.ChangeState(Attacked); break;
            case TitanState.Die: SM.ChangeState(Die); break;
            case TitanState.Lunge: SM.ChangeState(Lunge); break;
            case TitanState.Tremor: SM.ChangeState(Tremor); break;

        }
    }
    private bool AttackProcessing()
    {
        if (TC.Attack == -1) return false;
        if (SM.IsFinished)
        {
            AttackOver();
            if (skilled)//原来就是无敌就不用开启受击
                attackedBox.gameObject.SetActive(false);
            return false;
        }
        return true;
    }
    private void AttackOver()
    {
        TC.AttackEnd();
        TC.AttackOver();
        TM.LungeOver();
        TM.Stop();
        attackedBox.gameObject.SetActive(true);
    }
    private void CheckLunge()
    {
        if (TM.StopLunge)
        {
            AttackOver();return;
        }
        bool priR = TM.AttackFaceRight;
        bool r = TM.Lunging();
        SM.ChangeSpriteFace(r);
        TC.ChangeAttackBox(r);
        if (priR != r)
            TC.UpdateAttack();
    }
    private bool AttackedProcessing()
    {
        if (TH.Boost || state != TitanState.Attacked) return false;
        if (SM.IsFinished)
        {
            TM.Stop();
            return false;
        }
        return true;
    }
    private bool DieProcessing()
    {
        if (state != TitanState.Die) return false;
        if (SM.IsFinished) gameObject.SetActive(false);
        return true;
    }
    private void ObtainBoost()//无法选中
    {
        TH.AttackedEnd();
        attackedBox.gameObject.SetActive(false);
        SM.SetYellow();//被黄色光包裹
        SM.SetAngry(true);
        skilled = true;
        beginBoostTime = Time.time;
    }
    private void LoseBoost()//失去技能增益
    {
        attackedBox.gameObject.SetActive(true);
        skilled = false;
        SM.SetAngry(false);
    }
    private bool BoostProcessing()
    {
        if (state != TitanState.Boost) return false;
        if (SM.IsFinished)
        {
            ObtainBoost();
            return false;
        }
        return true;
    }
    private void ObtainAngry()
    {
        TC.AngryRequest = false;
        TC.Boost();
        SM.SetRed();
        SM.SetAngry(true);
        TM.Boost();
    }
    private void LoseAngry()
    {
        TC.BoostOver();
        SM.SetAngry(false);
        TM.ReturnSpeed();
    }
}
