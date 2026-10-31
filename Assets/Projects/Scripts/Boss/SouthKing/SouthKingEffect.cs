using UnityEngine;

//增长天王独立特效共用生命周期和目标查询；离手后不依赖本体继续播放施法动画。
public abstract class SouthKingEffect : MonoBehaviour
{
    [SerializeField] protected SpriteData frames;//已切割的完整精灵序列。
    [SerializeField] protected Rect[] frameBounds;//每帧真实非透明范围，相对pivot，以Unity单位记录。
    protected SpriteRenderer sr;
    protected SouthKingCombat owner;
    protected Health ownerHealth;
    protected PlayerManager players;
    protected int ownerLife;
    protected float beganAt, damageScale;
    protected bool active, right, hit;
    protected virtual bool ReportsAttackResult => true;
    protected bool ValidOwner => owner != null && owner.isActiveAndEnabled && ownerHealth != null
        && !ownerHealth.Dead && ownerHealth.LifeVersion == ownerLife;
    public bool BelongsTo(SouthKingCombat source) => active && owner == source && ownerHealth != null && ownerHealth.LifeVersion == ownerLife;
    public void Launch(SouthKingCombat source, PlayerManager party, Vector2 position, bool faceRight)
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        owner = source; ownerHealth = source != null ? source.GetComponent<Health>() : null;
        ownerLife = ownerHealth != null ? ownerHealth.LifeVersion : -1;
        players = party; right = faceRight; active = true; hit = false;
        beganAt = Time.time; damageScale = source != null ? source.DamageMultiplier : 1f;
        transform.position = new Vector3(position.x, position.y, 0f);
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        SpriteRenderer body = source != null ? source.transform.Find("Visual")?.GetComponent<SpriteRenderer>() : null;
        if (sr != null)
        {
            sr.enabled = true; sr.flipX = !faceRight; sr.color = Color.white;
            if (body != null) { sr.sortingLayerID = body.sortingLayerID; sr.sortingOrder = body.sortingOrder + 1; }
        }
        OnLaunched();
        if (!ValidOwner || !BattleRun.IsRunning) StopEffect();
    }
    protected virtual void OnLaunched() { }
    protected abstract void Tick(float elapsed);
    private void Update()
    {
        if (!active) return;
        if (!BattleRun.IsRunning || !ValidOwner) { StopEffect(); return; }
        Tick(Mathf.Max(0f, Time.time - beganAt));
    }
    protected int Count => frames != null && frames.sprites != null ? frames.sprites.Length : 0;
    protected float Fps => frames != null ? Mathf.Max(1f, frames.fps) : 12f;
    protected void Show(int frame)
    {
        if (sr != null && Count > 0) sr.sprite = frames.sprites[Mathf.Clamp(frame, 0, Count - 1)];
    }
    protected bool Area(int frame, out Bounds area)
        => TangSkillEffectUtility.TryBounds(transform, sr, frameBounds, frame, out area);
    protected bool TryPlayer(out PlayerController player, out Health health, out Collider2D body)
    {
        player = players != null ? players.CurrentPlayer : null;
        health = player != null ? player.GetComponent<Health>() : null;
        body = player != null ? player.GetComponent<Collider2D>() : null;
        return player != null && player.isActiveAndEnabled && player.CanReceiveHit && health != null && !health.Dead
            && body != null && body.enabled && body.gameObject.activeInHierarchy;
    }
    public static float GroundAt(Vector2 position)
    {
        RaycastHit2D ground = Physics2D.Raycast(position + Vector2.up * 25f, Vector2.down, 100f, 1 << LayerMask.NameToLayer("Ground"));
        return ground.collider != null ? ground.point.y : position.y;//独立测试缺少地板时保持调用者给定高度。
    }
    public void StopEffect()
    {
        if (!active) return;
        active = false;
        if (ReportsAttackResult && ValidOwner && BattleRun.IsRunning) owner.ReportEffectResult(hit, ownerLife);
        if (gameObject.activeSelf) gameObject.SetActive(false);
    }
    protected virtual void OnStopped() { }
    private void OnDisable()
    {
        active = false; OnStopped();
        if (sr != null) sr.enabled = false;
        owner = null; ownerHealth = null; players = null;
    }
}
