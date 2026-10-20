using System;
using UnityEngine;

public class PlayerCombat : MonoBehaviour
{
    [SerializeField] private BoxCollider2D AttackBox,RiseBox;//普攻和上升攻击的攻击箱
    [SerializeField] private BoxCollider2D[] SkillBoxes;//技能攻击箱
    [SerializeField] private BoxCollider2D[] ComboBoxes;//组合技攻击箱

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
        if (Skilled != -1 || SkillRequest) return;
        Skilled = k;
        SkillRequest = true;
    }
    public void OpenAttackBox(bool r)
    {
        OpenBox(AttackBox, r);
    }
    public void ShutAttackBox()
    {
        if (Comboed!=-1&&AttackBox.gameObject.activeSelf)
            AttackBox.gameObject.SetActive(false);
    }
    public void OpenComboBox(bool r)
    {
        OpenBox(ComboBoxes[Comboed], r);
    }
    public void ShutComboBox()
    {
        if (Comboed!=-1&&ComboBoxes[Comboed].gameObject.activeSelf)
            ComboBoxes[Comboed].gameObject.SetActive(false);
    }
    public void OpenSkillBox(bool r)
    {
        OpenBox(SkillBoxes[Skilled], r);
    }
    public void ShutSkillBox()
    {
        if (Skilled!=-1&&SkillBoxes[Skilled].gameObject.activeSelf)
            SkillBoxes[Skilled].gameObject.SetActive(false);
    }
    public void OpenRiseBox(bool r)
    {
        OpenBox(RiseBox, r);
    }
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
}
