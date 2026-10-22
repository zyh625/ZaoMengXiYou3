using System.Collections.Generic;
using UnityEngine;

public class AttackBox : MonoBehaviour
{
    [SerializeField] private float Attack;
    protected float curAttack;
    protected readonly HashSet<IDamage> hitEnemies = new();
    protected virtual void OnEnable()
    {
        curAttack = Attack;
        hitEnemies.Clear();
    }
    protected void OnTriggerStay2D(Collider2D collider) => TryAttack(collider);
    protected void OnTriggerEnter2D(Collider2D collider) => TryAttack(collider);
    protected virtual void TryAttack(Collider2D collider)
    {

    }
    public void UpdateAttack() => hitEnemies.Clear();
    public void ChangeAttack(float ratio) => curAttack *= ratio;
    public void ReturnBegin() => curAttack = Attack;
}
