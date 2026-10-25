using Unity.VisualScripting;
using UnityEngine;

public class TitanController : EnemyController
{
    [SerializeField]
    private SpriteData Boost, Lunge, Attack, Tremor;
    private TitanMovement TM;

    [SerializeField] private float boostRatio, boostDuration;
    [SerializeField] private float[] bloodRatio;//血量降低到对应阈值使用强化，可被打断，短暂无敌
    private int curBoostTime = 0;
    private float beginBoostTime = -1f;
    int skilled = 0;
    protected override void Awake()
    {
        base.Awake();
        TM = GetComponent<TitanMovement>();
    }
    protected override void OnEnable()
    {
        base.OnEnable();
        curBoostTime = 0;
    }
    protected override bool SkillBoost => skilled == 1;
    protected override void CheckSkillBoostLose()
    {
        if (Time.time - beginBoostTime >= boostDuration)
        {
            attackedBox.gameObject.SetActive(true);
            skilled = 0;
            SM.SetAngry(false);
        }
    }
    protected override bool SkillReady => !EH.Boost && state != EnemyState.Skill &&
        curBoostTime < bloodRatio.Length && EH.ratio <= bloodRatio[curBoostTime];
    protected override void SkillSet()=> curBoostTime++;
    protected override void HandleSkill() => SM.ChangeState(Boost);
    protected override void HandleAttack(int attack)
    {
        switch (attack)
        {
            case 0:SM.ChangeState(Attack);break;
            case 1:SM.ChangeState(Lunge);break;
            case 2:SM.ChangeState(Tremor);break;
        }
    }
    protected override void SkillOver()
    {
        EH.AttackedEnd();
        attackedBox.gameObject.SetActive(false);
        SM.SetYellow();//被黄色光包裹
        SM.SetAngry(true);
        skilled = 1;
        beginBoostTime = Time.time;
    }
    protected override void CheckSpecialAttack()
    {
        if (TM.StopLunge)
        {
            AttackOver(); return;
        }
        bool priR = TM.AttackFaceRight;
        bool r = TM.Lunging();
        SM.ChangeSpriteFace(r);
        EC.ChangeAttackBox(r);
        if (priR != r)
            EC.UpdateAttack();
    }
    protected override void SpecialAttackSet(float dis)
    {
        TM.AttackFaceRight = dis > 0f;
        attackedBox.gameObject.SetActive(false);//攻击过程无敌
    }
    protected override void SpecialAttackOver()
    {
        TM.LungeOver();
        attackedBox.gameObject.SetActive(true);
        TM.Stop();
    }
    protected override void SpecialOver()
    {
        if (SkillBoost)
            attackedBox.gameObject.SetActive(false);
    }
}
