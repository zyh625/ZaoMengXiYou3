using UnityEngine;

public class EnemyCombat : MonoBehaviour
{
    [SerializeField] private BoxCollider2D[] AttackBoxes;//攻击一二的碰撞箱
    [SerializeField] private float[] attackDelay;//两种招式的冷却
    [SerializeField] private EnemyCheckAttack[] CheckAttackLogic;//攻击一二的检查逻辑
    private BoxCollider2D[] CheckAttackBoxes = new BoxCollider2D[2];//攻击一二的检查箱
    [SerializeField] private int[] AttackOpenIdx, AttackShutIdx;
    public bool AttackOpen(int idx) => idx >= AttackOpenIdx[curAttack] 
        && idx < AttackShutIdx[curAttack];
    public bool AttackShut(int idx) => idx >= AttackShutIdx[curAttack];
    private float[] lastAttack;
    public bool[] AttackRequest = new bool[2];
    private void OnEnable() => lastAttack = new float[] { -1, -1 };
    private int curAttack = -1;
    public int Attack => curAttack;
    public void AttackOver() => curAttack = -1;
    public bool AttackReady => AttackRequest[0]||AttackRequest[1];
    private void Start()
    {
        CheckAttackBoxes[0] = CheckAttackLogic[0].GetComponent<BoxCollider2D>();
        CheckAttackBoxes[1] = CheckAttackLogic[1].GetComponent<BoxCollider2D>();
    }
    public void CheckAttack()
    {
        if (Attack != -1) return;
        AttackRequest[0] = CheckBox(0);
        AttackRequest[1] = CheckBox(1);
    }
    private bool CheckBox(int i)
    {
        return CheckAttackLogic[i].PlayerIn && (lastAttack[i] == -1
            || Time.time - lastAttack[i] >= attackDelay[i]);
    }
    public void HasAtatck(bool r)
    {
        int i = AttackRequest[0] ? 0 : 1;
        curAttack = i;
        lastAttack[i] = Time.time;
        ChangeBoxFace(AttackBoxes[i], r);
    }
    public void AttackBegin()
    {
        if (!AttackBoxes[curAttack].gameObject.activeSelf)
            AttackBoxes[curAttack].gameObject.SetActive(true);
    }
    public void AttackEnd()
    {
        if (curAttack!=-1&&AttackBoxes[curAttack].gameObject.activeSelf)
            AttackBoxes[curAttack].gameObject.SetActive(false);
    }
    public void ChangeCheckBox(bool r)
    {
        ChangeBoxFace(CheckAttackBoxes[0], r);
        ChangeBoxFace(CheckAttackBoxes[1], r);
    }
    private void ChangeBoxFace(BoxCollider2D box,bool r)
    {
        Vector2 offset = box.offset;
        offset.x = Mathf.Abs(offset.x) * (r ? 1f : -1f);
        box.offset= offset;
    }
    public void UpdateAngry()//攻击增加战意
    {

    }

}
