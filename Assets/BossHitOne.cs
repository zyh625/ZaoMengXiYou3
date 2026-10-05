using UnityEngine;

public class BossHitOne : MonoBehaviour
{
    private bool hasHit = false;
    private void OnEnable()
    {
        hasHit = false;
    }
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasHit) return;
        if (!other.CompareTag("Player")) return;
        Debug.Log("bosshit");
        hasHit = true;
        Player player= other.GetComponent<Player>();
        player.TakeDamage(Boss1.attackOne); 
    }
}
