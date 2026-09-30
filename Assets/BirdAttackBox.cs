using UnityEngine;

public class BirdAttackBox : MonoBehaviour
{
    private bool hasHit;
    private void OnEnable()
    {
        hasHit = false;
    }
    private void OnTriggerStay2D(Collider2D other)
    {
        if (hasHit || !other.CompareTag("Player")) return;
        Debug.Log("attacked");
        Player p = other.GetComponent<Player>();
        if (p != null)
        {
            hasHit = true;
            Player.attackedDirect = new Vector2(p.transform.position.x >= transform.position.x ? 1f : -1f, 0f);
            p.TakeDamage(Bird.attack);
        }
    }
}
