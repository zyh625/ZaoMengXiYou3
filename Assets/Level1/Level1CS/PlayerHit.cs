using System.Collections.Generic;
using UnityEngine;

public class PlayerHit : MonoBehaviour
{
    private readonly HashSet<IDamageable> hitEnemies = new();
    private Player player;
    private void Start()
    {
        player = GetComponentInParent<Player>();
    }
    private void OnEnable()
    {
        hitEnemies.Clear();
    }
    private void OnTriggerEnter2D(Collider2D other)
    {
        IDamageable enemy = other.GetComponentInParent<IDamageable>();
        if (enemy == null) return;//不是敌人
        if (!hitEnemies.Add(enemy)) return;//已经攻击过了
        if (player != null)
            player.UpdateBoost(true);
        enemy.TakeDamage(Player.attack);
    }
}
