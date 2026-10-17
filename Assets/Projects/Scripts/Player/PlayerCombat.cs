using System;
using UnityEngine;

public class PlayerCombat : MonoBehaviour
{
    [SerializeField] private BoxCollider2D AttackLeftBox, AttackRightBox;//普攻和组合技的攻击箱
    [SerializeField] private BoxCollider2D SkillOneBox, SkillTwoBox, SkillThreeBox;//技能攻击箱

    public bool attackRequest = false;//请求攻击
    private float attackPri = -1f;//最多保存一次攻击
    private float attackCur = -1f;//最近一次按下攻击的时间
    private float attackValid= 0.8f;//攻击请求有效期
    private int curAttack = -1;//下次攻击的招式的前一招
    public int CurAttack => curAttack;
    private bool isAttacking;//普通攻击
    public bool IsAttacking => isAttacking;
    private float lastAttack = -1f;
    [SerializeField] private float attackDelay = 0.7f;//攻击间隔

    public void OnAttack()
    {
        attackRequest = true;
        attackPri = attackCur;
        attackCur = Time.time;
    }
    public bool NormalAttack()
    {
        if (lastAttack != -1f && Time.time - lastAttack <= attackDelay) return false;
        if (attackRequest)
        {
            attackRequest = false;
            if (Time.time - attackCur <= attackValid)
            {
                HasAttack();
                return true;
            }
        }
        else if (attackPri != -1f && Time.time - attackPri <= attackValid)
        {
            HasAttack();
            return true;
        }
        return false;
    }
    private void HasAttack()
    {
        lastAttack= Time.time;
        attackPri = attackCur;
        attackCur = -1f;
        curAttack = curAttack == 2 ? 0 : curAttack + 1;
        isAttacking = true;
    }
    public void DisruptAttack()
    {
        isAttacking = false;
    }
}
