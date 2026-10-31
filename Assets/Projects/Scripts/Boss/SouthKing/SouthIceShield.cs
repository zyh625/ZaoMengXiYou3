using UnityEngine;

//护盾不另挂Health，所有伤害由Boss的同一次命中结算入口优先交给耐久处理。
public class SouthIceShield : SouthKingEffect
{
    [SerializeField, Min(0.01f)] private float durability = 120f;//独立盾耐久，当前命中全部由盾吸收，击破后下一击才扣本体血量。
    [SerializeField, Min(0f)] private float duration;//0表示直到击破；需要限时盾时可配置秒数。
    [SerializeField, Min(0.1f)] private float sizeRatio = 1.15f;//根据本体图片自动覆盖，无需角色换帧时重建。
    [SerializeField, Min(0.1f)] private float bodyHeightRatio = 1.65f;//碰撞身体到完整立姿的高度比例，避免剑光画布把护盾撑得过大。
    private float remaining, fadeAt;
    private bool fading;
    private SpriteRenderer bodyRenderer;
    public bool Protecting => active && !fading && remaining > 0f;
    protected override bool ReportsAttackResult => false;
    protected override void OnLaunched()
    {
        remaining = Mathf.Max(0.01f, durability); fading = false;
        bodyRenderer = owner != null ? owner.transform.Find("Visual")?.GetComponent<SpriteRenderer>() : null;
        Place(); Show(0);
    }
    private void Place()
    {
        if (bodyRenderer == null || Count == 0 || frames.sprites[0] == null) return;
        Collider2D bodyCollider = owner != null ? owner.GetComponent<Collider2D>() : null;
        Bounds body = bodyCollider != null ? bodyCollider.bounds : bodyRenderer.bounds;
        float height = body.size.y * (bodyCollider != null ? Mathf.Max(0.1f, bodyHeightRatio) : 1f);
        transform.position = new Vector3(body.center.x, body.min.y + height * 0.5f, owner.transform.position.z);
        float size = height * Mathf.Max(0.1f, sizeRatio);
        //逐帧贴边裁切后取整套动画的最大尺寸，避免第一帧裁小后把护盾整体放大。
        float spriteSize = 0.01f;
        for (int i = 0; i < Count; i++)
            if (frames.sprites[i] != null)
                spriteSize = Mathf.Max(spriteSize, frames.sprites[i].bounds.size.x, frames.sprites[i].bounds.size.y);
        transform.localScale = Vector3.one * (size / spriteSize);
        if (sr != null) { sr.flipX = false; sr.sortingOrder = bodyRenderer.sortingOrder + 1; }
    }
    public bool Absorb(DamageInfo damage)
    {
        if (!Protecting || damage.Amount <= 0f) return false;
        remaining = Mathf.Max(0f, remaining - damage.Amount);
        if (remaining <= 0f) BeginFade();
        return true;
    }
    private void BeginFade() { if (!fading) { fading = true; fadeAt = Time.time; Show(12); } }
    protected override void Tick(float elapsed)
    {
        if (Count < 16) { StopEffect(); return; }
        Place();
        if (duration > 0f && elapsed >= duration) BeginFade();
        if (fading)
        {
            int frame = 12 + Mathf.FloorToInt((Time.time - fadeAt) * Fps);
            if (frame >= 16) StopEffect(); else Show(frame);
        }
        else
        {
            float ratio = remaining / Mathf.Max(0.01f, durability);
            int stage = ratio > 2f / 3f ? 0 : ratio > 1f / 3f ? 1 : 2;
            Show(stage * 4 + Mathf.FloorToInt(elapsed * Fps) % 4);
        }
    }
    protected override void OnStopped() { remaining = 0f; fading = false; bodyRenderer = null; }
}
