using UnityEngine;

public class EnemyHitBox : MonoBehaviour
{
    private bool hasHit;
    private void OnEnanble()
    {
        hasHit = false;
    }
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasHit || !other.CompareTag("Player")) return;
        Player p = other.GetComponent<Player>();
        if(p != null )
        {
            hasHit = true;
            Player.attackedDirect = new Vector2(p.transform.position.x >= transform.position.x ? 1f : -1f, 0f);
            p.TakeDamage(Boss1.attack);
        }
    }
}
