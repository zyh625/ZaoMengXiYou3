using System;
using UnityEngine;

public class EnemyCombat : MonoBehaviour
{
    [SerializeField] private BoxCollider2D[] AttackBoxes;//攻击的碰撞箱
    private EnemyAttackBox[] enemyAttackBoxes;
    [SerializeField] private float[] attackDelay;//攻击冷却
    private float[] curAttackDelay;//当前攻击冷却
    [SerializeField] private EnemyCheckAttack[] CheckAttackLogic;//攻击的检查逻辑
    private BoxCollider2D[] CheckAttackBoxes;//攻击的检查箱
    [SerializeField] private int[] AttackOpenIdx, AttackShutIdx;
    public bool AttackOpen(int idx) => idx >= AttackOpenIdx[curAttack] 
        && idx < AttackShutIdx[curAttack];
    public bool AttackShut(int idx) => idx >= AttackShutIdx[curAttack];
    private float[] lastAttack;
    public bool[] AttackRequest;
    private int attackNum;
    private int curAttack = -1;
    public int Attack => curAttack;
    public void AttackOver() => curAttack = -1;
    [SerializeField] private float angryFull, angryRatio,deltaAngry,angryDuration,delayRatio;//怒气总量，愤怒状态下的攻击增益
    [SerializeField]private float curAngry;
    private bool angry = false;
    public bool Angry => angry;
    public bool AngryRequest = false;
    private float lastAngry = -1f;
    public bool AngryEnd=> (Time.time - lastAngry) >= angryDuration;
    public bool AttackReady()//是否能攻击
    {
        for(int i=0;i<attackNum;i++)
            if (AttackRequest[i])return true;
        return false;
    }
    public bool AttackFaceRight;
    private void Awake()
    {
        attackNum = AttackBoxes.Length;
        lastAttack = new float[attackNum];
        AttackRequest = new bool[attackNum];
        curAttackDelay = new float[attackNum];
        CheckAttackBoxes=new BoxCollider2D[attackNum];
        enemyAttackBoxes=new EnemyAttackBox[attackNum];
        for (int i = 0; i < attackNum; i++)
        {
            CheckAttackBoxes[i] = CheckAttackLogic[i].GetComponent<BoxCollider2D>();
            enemyAttackBoxes[i] = AttackBoxes[i].GetComponent<EnemyAttackBox>();
            curAttackDelay[i] = attackDelay[i];
        }
    }
    private void OnEnable()
    {
        angry = false;
        for(int i = 0; i < attackNum; i++)
        {
            AttackBoxes[i].gameObject.SetActive(false);
            enemyAttackBoxes[i].ReturnBegin();
            curAttackDelay[i] = attackDelay[i];
            lastAttack[i] = Time.time;//开始都进入冷却，给玩家反应时间
            AttackRequest[i] = false;
        }
    }
    public void CheckAttack()//检查当前是否满足攻击条件
    {
        if (Attack != -1) return;
        for (int i = 0; i < attackNum; i++)
            AttackRequest[i] = CheckAttackLogic[i].PlayerIn &&
            Time.time - lastAttack[i] >= curAttackDelay[i];
    }
    public void HasAtatck(bool r)//成功攻击
    {
        int cur = -1;
        for(int i=0; i<attackNum; i++)
            if (AttackRequest[i])
            {
                cur = i;break;
            }
        curAttack = cur;
        lastAttack[cur] = Time.time;
        AttackFaceRight = r;
        ChangeBoxFace(AttackBoxes[cur], r);
    }
    public void AttackBegin()//开始有效攻击
    {
        if (!AttackBoxes[curAttack].gameObject.activeSelf)
            AttackBoxes[curAttack].gameObject.SetActive(true);
    }
    public void AttackEnd()//有效攻击结束
    {
        if (curAttack!=-1&&AttackBoxes[curAttack].gameObject.activeSelf)
            AttackBoxes[curAttack].gameObject.SetActive(false);
    }
    public void ChangeCheckBox(bool r)//改变检查箱方向
    {
        for (int i = 0; i < attackNum; i++)
            ChangeBoxFace(CheckAttackBoxes[i], r);
    }
    private void ChangeBoxFace(BoxCollider2D box,bool r)
    {
        Vector2 offset = box.offset;
        offset.x = Mathf.Abs(offset.x) * (r ? 1f : -1f);
        box.offset= offset;
    }
    public void ChangeAttackBox(bool r) => ChangeBoxFace(AttackBoxes[curAttack], r);
    public void UpdateAttack() => enemyAttackBoxes[curAttack].UpdateAttack();//此时可以再次对玩家造成伤害
    public void UpdateAngry()//攻击、受击增加怒气
    {
        curAngry += deltaAngry;
        if (curAngry >= angryFull)
        {
            curAngry = 0;
            AngryRequest = true;
        }
    }
    public void Boost()//愤怒模式
    {
        lastAngry = Time.time;
        angry = true;
        for (int i = 0; i < attackNum; i++)
        {
            enemyAttackBoxes[i].ChangeAttack(angryRatio);
            curAttackDelay[i] *= delayRatio;
        }
    }
    public void BoostOver()//愤怒模式结束
    {
        angry = false;
        for (int i = 0; i < attackNum; i++)
        {
            enemyAttackBoxes[i].ReturnBegin(); 
            curAttackDelay[i] = attackDelay[i];
        }
    }
}
