using UnityEngine;

//生长期间冰冻接触者，进入碎裂片段时对仍持有本来源冰块的玩家结算一次伤害。
public class SouthIceSpikes : SouthKingEffect
{
    [SerializeField, Min(0f)] private float damage = 16f, advanceSpeed = 2f;//碎裂基础伤害与地面向前推进的速度。
    [SerializeField, Min(1)] private int activeFrames = 6;//前六帧有效，之后四帧碎裂消散。
    private Health frozenPlayer;
    private int frozenLife, previousFrame;
    private Vector2 origin;
    private bool shattered;
    protected override void OnLaunched()
    {
        frozenPlayer = null; frozenLife = -1; previousFrame = -1; shattered = false;
        origin = transform.position; Show(0);
    }
    protected override void Tick(float elapsed)
    {
        if (Count == 0) { StopEffect(); return; }
        int frame = Mathf.Min(Count, Mathf.FloorToInt(elapsed * Fps));
        Vector3 position = transform.position;
        position.x = origin.x + (right ? 1f : -1f) * Mathf.Max(0f, advanceSpeed) * Mathf.Min(elapsed, activeFrames / Fps);
        position.y = GroundAt(new Vector2(position.x, origin.y)); transform.position = position;
        //逐一补采样跨过的生长帧，不能因低帧率直接跳到碎裂而漏掉冻结。
        for (int i = Mathf.Max(0, previousFrame + 1); i <= Mathf.Min(frame, activeFrames - 1); i++) Freeze(i);
        if (frame < activeFrames) Freeze(frame);
        else if (!shattered) Shatter();
        previousFrame = frame;
        if (!active) return;
        if (frame >= Count) { StopEffect(); return; }
        Show(frame);
    }
    private void Freeze(int frame)
    {
        if (shattered || frozenPlayer != null || !Area(frame, out Bounds area)
            || !TryPlayer(out _, out Health health, out Collider2D body) || !area.Intersects(body.bounds)) return;
        float remaining = Mathf.Max(0.1f, activeFrames / Fps - (Time.time - beganAt) + 0.2f);
        if (health.Status != null && health.Status.ApplyFreeze(this, remaining))
        { frozenPlayer = health; frozenLife = health.LifeVersion; }
    }
    private void Shatter()
    {
        shattered = true;
        Health target = frozenPlayer;
        if (target == null || !target.isActiveAndEnabled || target.Dead || target.LifeVersion != frozenLife
            || target.Status == null || !target.Status.HasFreeze(this)) return;
        PlayerController player = target.GetComponent<PlayerController>();
        if (player == null || !player.CanReceiveHit) { target.Status.CancelFreeze(this); return; }
        float blood = target.CurBlood;
        target.TakeDamage(new DamageInfo(owner.transform, Mathf.Max(0f, damage) * damageScale, false, kind: DamageKind.Skill));
        hit = target.CurBlood < blood;
        target.Status?.CancelFreeze(this);//被免伤挡住也不能留下已经消散冰阵的控制来源。
    }
    protected override void OnStopped()
    {
        if (frozenPlayer != null && frozenPlayer.LifeVersion == frozenLife) frozenPlayer.Status?.CancelFreeze(this);
        frozenPlayer = null;
    }
}
