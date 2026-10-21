using System;
using System.Security.Cryptography;
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
    [SerializeField] private Transform target;
    public float DeltaX => target.position.x - transform.position.x;
    private bool attackedFaceRight;
    public bool AttackedFaceRight => attackedFaceRight;
    void Start()
    {
        curBlood = beginBlood;
    }
    void OnEnable()
    {
        die = false;attacked = false;attackedUp = false;
    }
    public void TakeDamage(float damage)
    {
        if (die) return;
        attackedFaceRight = DeltaX >= 0f;
        attackedUp = damage < 0;
        damage = Mathf.Abs(damage);
        curBlood = Mathf.Max(0f, curBlood - damage);
        Debug.Log(curBlood);
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
