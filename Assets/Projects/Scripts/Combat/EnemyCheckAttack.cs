using UnityEngine;
using System.Collections.Generic;

public class EnemyCheckAttack : MonoBehaviour
{
    private readonly HashSet<Collider2D> players = new();//只登记玩家本体，攻击特效和其他敌人不能触发出招
    private Collider2D detector;//退出回调可能因停用或直接位移缺失，读取时再次核对当前几何重叠
    public bool PlayerIn
    {
        get
        {
            if (detector == null) detector = GetComponent<Collider2D>();
            if (detector == null || !detector.enabled || !gameObject.activeInHierarchy) return false;
            foreach (Collider2D player in players)
                if (IsPlayerBody(player) && detector.Distance(player).isOverlapped) return true;
            return false;
        }
    }
    private static bool IsPlayerBody(Collider2D hit)
    {
        if (hit == null || !hit.enabled || !hit.gameObject.activeInHierarchy) return false;
        PlayerController player = hit.GetComponentInParent<PlayerController>();
        return player != null && player.isActiveAndEnabled
            && (hit == player.GetComponent<Collider2D>() || hit.gameObject.layer == LayerMask.NameToLayer("Player"))
            && player.GetComponent<Health>() is Health health && !health.Dead;
    }
    private void OnTriggerEnter2D(Collider2D player) { if (IsPlayerBody(player)) players.Add(player); }
    private void OnTriggerStay2D(Collider2D player) { if (IsPlayerBody(player)) players.Add(player); }
    private void OnTriggerExit2D(Collider2D player) => players.Remove(player);
    private void OnDisable() => players.Clear();//换人和对象池停用不会留下上一轮范围记录
}
