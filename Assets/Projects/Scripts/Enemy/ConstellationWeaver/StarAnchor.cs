using UnityEngine;

[RequireComponent(typeof(Health))]
public sealed class StarAnchor : MonoBehaviour
{
    [SerializeField] private EffectFramePlayer visual;//星锚图：出现(F1–5)后循环保持脉冲(F6–11)
    private ConstellationWeaverCombat owner;
    private Health health;
    private void Awake() { health = GetComponent<Health>(); health.Died += Broken; }
    public void Place(ConstellationWeaverCombat source, Vector2 point)
    { owner = source; transform.position = point; gameObject.SetActive(true); if (visual != null) visual.Play(0, 10, 5, 10); }
    private void Broken(DamageInfo damage) { var source = owner; owner = null; source?.AnchorBroken(); }
    public void Recycle() { owner = null; gameObject.SetActive(false); }
    private void OnDisable() { var source = owner; owner = null; source?.AnchorBroken(); }
    private void OnDestroy() { if (health != null) health.Died -= Broken; }
}
