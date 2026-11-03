using UnityEngine;

// A timed source on the victim survives trap recycling and removes only its own multiplier.
public sealed class ConstellationWeaverSlow : MonoBehaviour
{
    private PlayerMovement movement;
    private Health health;
    private float until;
    public static void Apply(Health victim, float duration)
    {
        if (victim.Dead) return;
        var slow = victim.GetComponent<ConstellationWeaverSlow>();
        if (slow == null) slow = victim.gameObject.AddComponent<ConstellationWeaverSlow>();
        slow.until = Mathf.Max(slow.until, Time.time + duration);
        slow.movement?.SetMoveMultiplier(slow, 0.6f);
    }
    private void Awake()
    {
        movement = GetComponent<PlayerMovement>(); health = GetComponent<Health>();
        health.DebuffsCleared += Clear;
    }
    private void Update() { if (Time.time >= until || health.Dead) Clear(); }
    private void Clear() { until = 0f; movement?.RemoveMoveMultiplier(this); }
    private void OnDisable() => Clear();
    private void OnDestroy() { if (health != null) health.DebuffsCleared -= Clear; Clear(); }
}
