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
    void OnEnable()
    {
        die = false;attacked = false;attackedUp = false;
        curBlood = beginBlood;
    }
    public void TakeDamage(Transform tran,float damage)
    {
        if (die) return;
        target = tran;
        attackedUp = damage < 0;
        damage = Mathf.Abs(damage);
        curBlood = Mathf.Max(0f, curBlood - damage);
        UpdateBloodUI();
        if (curBlood == 0f)
        {
            die = true;
            return;
        }
        attacked = true;
    }
    private void UpdateBloodUI()
    {

    }
}
