using UnityEngine;

public class PlayerCombat : MonoBehaviour
{
    [SerializeField] private BoxCollider2D AttackBox,RiseBox;//普攻和上升攻击的攻击箱
    [SerializeField] private BoxCollider2D[] SkillBoxes;//技能攻击箱
    [SerializeField] private BoxCollider2D[] ComboBoxes;//组合技攻击箱


    [SerializeField] private int[] ComboOpenIdx, ComboShutIdx, AttackOpenIdx, AttackShutIdx
        , SkillOpenIdx, SkillShutIdx;
    [SerializeField] private int RiseOpenIdx, RiseShutIdx, SkillThreeGroundOpenIdx;
    public bool ComboOpen(int idx,int k) => idx >= ComboOpenIdx[k] && idx < ComboShutIdx[k];
    public bool ComboShut(int idx, int k) => idx >= ComboShutIdx[k];
    public bool AttackOpen(int idx, int k) => idx >= AttackOpenIdx[k] && idx < AttackShutIdx[k];
    public bool AttackShut(int idx, int k) => idx >= AttackShutIdx[k];
    public bool SkillOpen(int idx, int k) => idx >= SkillOpenIdx[k] && idx < SkillShutIdx[k];
    public bool SkillShut(int idx, int k) => Skilled == 3 ? (idx >= SkillShutIdx[k]
        && idx < SkillThreeGroundOpenIdx) : idx >= SkillShutIdx[k];
    public bool RiseOpen(int idx) => idx >= RiseOpenIdx && idx < RiseShutIdx;
    public bool RiseShut(int idx) => idx >= RiseShutIdx;


    private bool attackRequest = false;//请求攻击
    private float attackPri = -1f;//最多保存一次攻击
    private float attackCur = -1f;//最近一次按下攻击的时间
    private bool pri = false;//这次普通攻击响应的是上次按下的还是最近按下的
    [SerializeField]private float attackValid= 0.8f;//攻击请求有效期
    public int Attacked = -1;
    public int Comboed = -1;
    private int curAttack = 0;//下次攻击的招式的前一招
    private float lastAttack = -1f;
    public bool AttackThisFrame = false;
    [SerializeField] private float attackDelay = 0.7f;//攻击间隔
    [SerializeField] private float attackMemory = 1.2f;//攻击连招保存
    public int Skilled = -1;
    [SerializeField] private int[] SkillThreeState;//到第几帧换阶段，整个运动过程类似尖三角
    public int SkillThreeCur = 0;//当前为技能三的哪一阶段，起跳->攻击->下落->起立
    private int skillThreeAttackIdx = -1;
    [SerializeField]private float skillThreeGroundRatio=1.5f;
    private bool attackedRequest=false;
    public bool AttackedRequest => attackedRequest;
    private int invincibleState = 0;
    public void SkillEnd() => invincibleState = 0;
    private bool SkillThreeBoxHasShut = true;
    [SerializeField] private int Skill1ImmuneBegin, Skill1ImmuneEnd, Skill3ImmuneBegin, Skill3ImmuneEnd;
    private PlayerAttackBox skillThreeBox;
    private void Start() => skillThreeBox = SkillBoxes[3].GetComponent<PlayerAttackBox>();
    public void OnAttack()
    {
        attackRequest = true;
        attackPri = attackCur;
        attackCur = Time.time;
    }
    public void CheckAttack()
    {
        AttackThisFrame = false;
        if (lastAttack != -1f && Time.time - lastAttack <= attackDelay) return;//攻击间隔过短
        if (attackPri != -1f && Time.time - attackPri <= attackValid)
        {
            AttackThisFrame = true;
            pri = true;
        }
        else if (attackRequest)
        {
            attackRequest = false;
            if (Time.time - attackCur <= attackValid)
            {
                AttackThisFrame = true;
                pri = false;
            }
        }
    }
    public void NormalAttack()
    {
        if (lastAttack != -1f && Time.time - lastAttack > attackMemory)
            curAttack = 1;
        else
            curAttack = curAttack == 3 ? 1 : curAttack + 1;
        Attacked = curAttack;
        lastAttack = Time.time;
        if (pri) attackPri = -1f;
        else attackCur = -1f;
    }
    public void OnSkill(int k)
    {
        if (Skilled != -1) return;
        skillThreeAttackIdx = -1;
        Skilled = k;
    }
    public void OpenAttackBox(bool r) => OpenBox(AttackBox, r);
    public void ShutAttackBox()
    {
        if (AttackBox.gameObject.activeSelf)
            AttackBox.gameObject.SetActive(false);
    }
    public void OpenComboBox(bool r) => OpenBox(ComboBoxes[Comboed], r);
    public void ShutComboBox()
    {
        if (Comboed!=-1&&ComboBoxes[Comboed].gameObject.activeSelf)
            ComboBoxes[Comboed].gameObject.SetActive(false);
    }
    public void OpenSkillBox(bool r) => OpenBox(SkillBoxes[Skilled], r);
    public void ShutSkillBox()
    {
        if (Skilled!=-1&&SkillBoxes[Skilled].gameObject.activeSelf)
            SkillBoxes[Skilled].gameObject.SetActive(false);
    }
    public void OpenRiseBox(bool r) => OpenBox(RiseBox, r);
    public void ShutRiseBox()
    {
        if (RiseBox.gameObject.activeSelf)
            RiseBox.gameObject.SetActive(false);
    }
    private void OpenBox(BoxCollider2D box,bool r)
    {
        Vector2 offset = box.offset;
        offset.x = Mathf.Abs(offset.x) * (r ? 1f : -1f);
        box.offset= offset;
        if (!box.gameObject.activeSelf)
            box.gameObject.SetActive(true);
    }
    public void CheckSkillThree(int idx)
    {
        if (SkillThreeCur == 0 && idx >= SkillThreeState[0] + 1)
            SkillThreeCur = 1;
        else if (SkillThreeCur == 1)
        {
            if (idx >= SkillThreeState[1] + 1) SkillThreeCur = 2;
            else if (idx != skillThreeAttackIdx)//斩击动画按帧叠加
            {
                skillThreeAttackIdx = idx;
                SkillBoxes[3].GetComponent<PlayerAttackBox>().UpdateAttack();
            }
        }
        else if (SkillThreeCur == 2 && idx >= SkillThreeState[2] + 1) SkillThreeCur = 3;
        if (SkillThreeCur == 3 && idx >= SkillThreeGroundOpenIdx
            && skillThreeAttackIdx< SkillThreeGroundOpenIdx)
        {
            if (skillThreeAttackIdx < SkillThreeGroundOpenIdx)
            {
                skillThreeBox.gameObject.SetActive(true);
                skillThreeBox.ChangeAttack(skillThreeGroundRatio);
                SkillThreeBoxHasShut = false;
            }
            skillThreeAttackIdx = idx;
            skillThreeBox.UpdateAttack();
        }
        if(SkillThreeCur == 3 && idx >= SkillThreeGroundOpenIdx + 1
            && skillThreeAttackIdx< SkillThreeGroundOpenIdx+1&&!SkillThreeBoxHasShut)//只开启一帧
        {
            skillThreeBox.gameObject.SetActive(false);
            skillThreeAttackIdx = idx;
            SkillThreeBoxHasShut = true;
        }
    }

    public int CheckInvincible(int idx)
    {
        if (invincibleState == 0 && ((Skilled == 1 && idx >= Skill1ImmuneBegin)
            || (Skilled == 3 && idx >= Skill3ImmuneBegin)))
            invincibleState = 1;
        else if (invincibleState == 1 && ((Skilled == 1 && idx >= Skill1ImmuneEnd)
            || (Skilled == 3 && idx >= Skill3ImmuneEnd)))
            invincibleState = 2;
        else if (invincibleState == 2 && ((Skilled == 1 && idx > Skill1ImmuneEnd)
            || (Skilled == 3 && idx > Skill3ImmuneEnd)))
            invincibleState = 3;
        return invincibleState;
    }
    public void UpdateBoost()//攻击到敌人，增加无双能量
    {

    }
}
