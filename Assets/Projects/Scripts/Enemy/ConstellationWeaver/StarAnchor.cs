using UnityEngine;

[RequireComponent(typeof(Health))]
public sealed class StarAnchor : MonoBehaviour
{
    private ConstellationWeaverCombat owner;
    private Health health;
    private void Awake() { health = GetComponent<Health>(); health.Died += Broken; }
    public void Place(ConstellationWeaverCombat source, Vector2 point)
    { owner = source; transform.position = point; gameObject.SetActive(true); }
    private void Broken(DamageInfo damage) { var source = owner; owner = null; source?.AnchorBroken(); }
    public void Recycle() { owner = null; gameObject.SetActive(false); }
    private void OnDisable() { var source = owner; owner = null; source?.AnchorBroken(); }
    private void OnDestroy() { if (health != null) health.Died -= Broken; }
}
