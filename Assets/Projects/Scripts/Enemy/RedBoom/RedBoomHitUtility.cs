using UnityEngine;

public static class RedBoomHitUtility
{
    public static ContactFilter2D PlayerFilter
    {
        get { ContactFilter2D filter = new(); filter.SetLayerMask(1 << 3); filter.useTriggers = true; return filter; }
    }
    public static Health PlayerHealth(Collider2D hit)
    {
        if (hit == null || !hit.enabled || !hit.gameObject.activeInHierarchy) return null;
        PlayerController player = hit.GetComponentInParent<PlayerController>();
        if (player == null || !player.isActiveAndEnabled || !player.CanReceiveHit) return null;
        Health health = player.GetComponent<Health>();
        return health != null && !health.Dead ? health : null;
    }
}
