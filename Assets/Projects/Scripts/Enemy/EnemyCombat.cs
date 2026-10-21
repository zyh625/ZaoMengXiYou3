using UnityEngine;

public class EnemyCombat : MonoBehaviour
{
    [SerializeField] private BoxCollider2D[] AttackBoxes;//攻击一二的碰撞箱
    [SerializeField] private float[] attackDelay;//两种招式的冷却
    private float[] lastAttack = new float[2];
    public bool[] AttackRequest = new bool[2];
    void Update()
    {

    }

}
