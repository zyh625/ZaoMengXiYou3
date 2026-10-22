using UnityEngine;

public class EnemyCheckAttack : MonoBehaviour
{
    private bool playerIn = false;
    public bool PlayerIn => playerIn;
    private void OnTriggerEnter2D(Collider2D player) => playerIn = true;
    private void OnTriggerStay2D(Collider2D player) => playerIn = true;
    private void OnTriggerExit2D(Collider2D player) => playerIn = false;
}
