using System;
using UnityEngine;

public class PlayerCombat : MonoBehaviour
{
    [SerializeField] private BoxCollider2D AttackLeftBox, AttackRightBox;//普攻和组合技的攻击箱
    [SerializeField] private BoxCollider2D SkillOneBox, SkillTwoBox, SkillThreeBox;//技能攻击箱

    private bool attackRequest = false;//请求攻击
    private float attackPri = -1f;//最多保存一次攻击
    private float attackCur = -1f;//最近一次按下攻击的时间
    [SerializeField]private float attackValid= 0.8f;//攻击请求有效期
    private int curAttack = -1;//下次攻击的招式的前一招
    public int CurAttack => curAttack;
    private float lastAttack = -1f;
    public bool AttackThisFrame = false;
    [SerializeField] private float attackDelay = 0.7f;//攻击间隔
    [SerializeField] private float attackMemory = 1.2f;//攻击连招保存
    public int Skilled = -1;
    public bool SkillRequest = false;

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
            HasAttack(true);
        else if (attackRequest)
        {
            attackRequest = false;
            if (Time.time - attackCur <= attackValid)
                HasAttack(false);
        }
    }
    private void HasAttack(bool pri)
    {
        if (lastAttack != -1f && Time.time - lastAttack > attackMemory) 
            curAttack = 0;
        else 
            curAttack = curAttack == 2 ? 0 : curAttack + 1;
        AttackThisFrame = true;
        lastAttack = Time.time;
        if (pri) attackPri = -1f;
        else attackCur = -1f;
    }
    public void OnSkill(int k)
    {
        if (Skilled != -1 || SkillRequest) return;
        Skilled = k;
        SkillRequest = true;
    }
}
