using UnityEngine;
using System.Collections.Generic;
public class PlayerAttackBox : MonoBehaviour
{
    [SerializeField] private float Attack;//不同招式造成的攻击不同
    private float curAttack;
    private PlayerCombat PC;
    private readonly HashSet<IDamage> hitEnemies = new();

    private void Start() => PC = GetComponentInParent<PlayerCombat>();
    private void OnEnable()
    {
        curAttack = Attack;
        hitEnemies.Clear();
    }
    private void OnTriggerStay2D(Collider2D collider) => TryAttack(collider);
    private void OnTriggerEnter2D(Collider2D collider) => TryAttack(collider);
    private void TryAttack(Collider2D collider)
    {
        IDamage enemy = collider.GetComponentInParent<IDamage>();
        if (enemy == null) return;
        if (!hitEnemies.Add(enemy)) return;
        if (PC != null)
            PC.UpdateBoost();
        enemy.TakeDamage(curAttack);
    }
    public void UpdateAttack() => hitEnemies.Clear();
    public void ChangeAttack(float ratio) => curAttack *= ratio;
    public void ReturnBegin() => curAttack = Attack;
}
