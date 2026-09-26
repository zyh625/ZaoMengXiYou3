using UnityEngine;

public class AttackHitBox : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Enemy"))//检测攻击到敌人
        {
            Bird b = collision.GetComponent<Bird>();
            if(b != null)
            {
                b.TakeDamage(Player.attack);
            }
        }
    }
}
