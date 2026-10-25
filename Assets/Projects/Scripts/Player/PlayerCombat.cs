using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Combat
{
    public string id;
    public SpriteData animation;
    public BoxCollider2D box;
    public int openFrame, closeFrame;

    public bool CanOpen(int idx) => idx >= openFrame && idx < closeFrame;
    public bool CanClose(int idx) => idx >= closeFrame;

    public void Open(int idx)
    {
        if (CanOpen(idx) && !box.gameObject.activeSelf)
            box.gameObject.SetActive(true);
    }

    public void Close(int idx)
    {
        if (CanClose(idx) && box.gameObject.activeSelf)
            box.gameObject.SetActive(false);
    }
}

public class PlayerCombat : MonoBehaviour
{
    [SerializeField] private Combat[] allAttacks;
    private int normalNum, comboNum, skillNum;
    private readonly Dictionary<string, Combat> actions = new();
    private Combat[] normalAttacks, comboAttacks, skills;//按编号缓存招式，0号留空，与Attacked、Comboed、Skilled的编号一致
    private Combat riseAttack;//空中普攻的数据，独立于地面普攻连招

    private bool attackRequest = false;//请求攻击
    private float attackPri = -1f;//最多保存一次攻击
    private float attackCur = -1f;//最近一次按下攻击的时间
    private bool pri = false;//这次普通攻击响应的是上次按下的还是最近按下的
    [SerializeField] private float attackValid = 0.8f;//攻击请求有效期
    public int Attacked = -1;
    public int Comboed = -1;
    private int curAttack = 0;//下次攻击的招式的前一招
    private float lastAttack = -1f;
    public bool AttackThisFrame = false;
    [SerializeField] private float attackDelay = 0.7f;//攻击间隔
    [SerializeField] private float attackMemory = 1.2f;//攻击连招保存
    public int Skilled = -1;
    private bool attackedRequest = false;
    public bool AttackedRequest => attackedRequest;

    protected virtual void Awake()
    {
        foreach (Combat action in allAttacks)
        {
            if (action == null || string.IsNullOrWhiteSpace(action.id))
            {
                Debug.LogError("存在未填写名称的招式", this);
                continue;
            }
            if (!actions.TryAdd(action.id, action))
                Debug.LogError($"招式{action.id}重名", this);
        }

        normalAttacks = GetActions("NormalAttack", out normalNum);
        comboAttacks = GetActions("ComboAttack", out comboNum);
        skills = GetActions("Skill", out skillNum);
        riseAttack = GetAction("RiseAttack1");
    }

    public Combat GetAction(string id) =>
        actions.TryGetValue(id, out var action) ? action : null;

    //只在初始化时按连续编号整理数据，战斗过程中直接通过数组访问。
    private Combat[] GetActions(string prefix, out int count)
    {
        List<Combat> result = new() { null };//0号留空，让招式编号从1开始
        count = 0;
        while (actions.TryGetValue($"{prefix}{count + 1}", out var action))
        {
            result.Add(action);
            count++;
        }
        return result.ToArray();
    }

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
            curAttack = curAttack == normalNum ? 1 : curAttack + 1;
        Attacked = curAttack;
        lastAttack = Time.time;
        if (pri) attackPri = -1f;
        else attackCur = -1f;
    }

    public virtual void OnSkill(int k)
    {
        if (Skilled != -1) return;
        Skilled = k;
    }

    public virtual void SkillEnd() { }

    public bool ComboOpen(int idx, int k) => comboAttacks[k].CanOpen(idx);
    public bool ComboShut(int idx, int k) => comboAttacks[k].CanClose(idx);
    public bool AttackOpen(int idx, int k) => normalAttacks[k].CanOpen(idx);
    public bool AttackShut(int idx, int k) => normalAttacks[k].CanClose(idx);
    public bool SkillOpen(int idx, int k) => skills[k].CanOpen(idx);
    public virtual bool SkillShut(int idx, int k) => skills[k].CanClose(idx);
    public bool RiseOpen(int idx) => riseAttack.CanOpen(idx);
    public bool RiseShut(int idx) => riseAttack.CanClose(idx);

    public void OpenAttackBox(bool r) => OpenBox(normalAttacks[Attacked].box, r);

    public void ShutAttackBox()
    {
        //普攻结束时Attacked也可能已经为-1，因此关闭所有普攻引用的攻击箱。
        for (int i = 1; i <= normalNum; i++)//i为从1开始的普攻编号
            ShutBox(normalAttacks[i].box);
    }

    public void OpenComboBox(bool r) => OpenBox(comboAttacks[Comboed].box, r);

    public void ShutComboBox()
    {
        if (Comboed != -1)
            ShutBox(comboAttacks[Comboed].box);
    }

    public void OpenSkillBox(bool r) => OpenBox(skills[Skilled].box, r);

    public void ShutSkillBox()
    {
        if (Skilled != -1)
            ShutBox(skills[Skilled].box);
    }

    public void OpenRiseBox(bool r) => OpenBox(riseAttack.box, r);
    public void ShutRiseBox() => ShutBox(riseAttack.box);

    private void OpenBox(BoxCollider2D box, bool r)
    {
        Vector2 offset = box.offset;
        offset.x = Mathf.Abs(offset.x) * (r ? 1f : -1f);
        box.offset = offset;
        if (!box.gameObject.activeSelf)
            box.gameObject.SetActive(true);
    }

    private void ShutBox(BoxCollider2D box)
    {
        if (box.gameObject.activeSelf)
            box.gameObject.SetActive(false);
    }

    public void UpdateBoost()//攻击到敌人，增加无双能量
    {

    }
}
