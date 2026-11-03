using System.Collections.Generic;
using UnityEngine;

public sealed class RedBoomMark : MonoBehaviour
{
    [SerializeField] private SpriteData appearance;
    [SerializeField] private SpriteManager spriteManager;
    private static readonly HashSet<RedBoomMark> active = new();
    private Health target;
    private Collider2D targetBody;
    private int life;
    private float expiresAt;
    public static bool IsMarked(Health health)
    {
        foreach (RedBoomMark mark in active)
            if (mark != null && mark.target==health && mark.gameObject.activeInHierarchy
                && Time.time<mark.expiresAt && health!=null && health.LifeVersion==mark.life) return true;
        return false;
    }
    public bool Apply(Health actor, SpriteRenderer source)
    {
        if (actor==null || actor.Dead || !actor.isActiveAndEnabled || actor.IsInvincible) return false;
        ClearTarget();
        target=actor;targetBody=actor.GetComponent<Collider2D>();life=actor.LifeVersion;expiresAt=Time.time+4f;
        target.SetDamageMultiplier(this,1.2f);
        target.DebuffsCleared+=Cancel;target.Died+=OnDied;
        SpriteRenderer renderer=spriteManager.GetComponent<SpriteRenderer>();
        if (source!=null) { renderer.sortingLayerID=source.sortingLayerID;renderer.sortingOrder=source.sortingOrder+3; }
        gameObject.SetActive(true);
        active.Add(this);
        Follow();spriteManager.ChangeState(appearance);
        return true;
    }
    private void Update()
    {
        if (target==null || target.Dead || !target.isActiveAndEnabled || target.LifeVersion!=life || Time.time>=expiresAt)
            Cancel();
    }
    private void LateUpdate() { if (target!=null) Follow(); }
    private void Follow() => transform.position = targetBody!=null
        ? new Vector3(targetBody.bounds.center.x,targetBody.bounds.max.y+0.35f,0f)
        : target.transform.position+Vector3.up*2.5f;
    private void OnDied(DamageInfo damage) => Cancel();
    public void Cancel() { if (gameObject.activeSelf) gameObject.SetActive(false); else ClearTarget(); }
    private void ClearTarget()
    {
        active.Remove(this);
        if (target!=null)
        {
            target.RemoveDamageMultiplier(this);
            target.DebuffsCleared-=Cancel;target.Died-=OnDied;
        }
        target=null;targetBody=null;
    }
    private void OnDisable() { ClearTarget();spriteManager.Pause(); }
    private void OnDestroy() => ClearTarget();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetMarks() => active.Clear();
}
