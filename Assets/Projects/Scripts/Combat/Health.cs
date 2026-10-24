using UnityEngine;

public class Health : MonoBehaviour,IDamage
{
    [SerializeField] private float beginBlood;
    [SerializeField]private float curBlood;
    private bool die, attacked;
    public bool Dead => die;
    public bool Attacked => attacked;
    public bool AttackedEnd() => attacked = false;
    [SerializeField]private bool attackedUp = false;
    public bool AttackedUp => attackedUp;
    private Transform target;
    public float DeltaX => target != null ? target.position.x - transform.position.x : 0f;
    public bool AttackedRight => DeltaX <= 0f;//向右边击退
    private bool slow = false;//当前是否被减速
    public bool Slow => slow;
    private bool boost = false;//当前是否免控
    public void SetBoost(bool flag)=>boost=flag;
    public bool Boost => boost;
    public float ratio => curBlood / beginBlood;
    void OnEnable()
    {
        die = false;attacked = false;attackedUp = false;
        curBlood = beginBlood;slow = false;
    }
    public void TakeDamage(Transform tran,float damage)
    {
        if (die) return;
        target = tran;
        attackedUp = damage < 0;
        damage = Mathf.Abs(damage);
        curBlood = Mathf.Max(0f, curBlood - damage);
        if (curBlood == 0f)
        {
            die = true;
            return;
        }
        attacked = true;
    }

}
