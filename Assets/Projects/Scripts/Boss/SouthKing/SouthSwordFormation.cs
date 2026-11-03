using System.Collections.Generic;
using UnityEngine;

//前六帧随下落播放，第六帧保持至着地，再播放末四帧；去程连续扫过玩家防止高速穿透。
public class SouthSwordFormation : SouthKingEffect
{
    [SerializeField, Min(0.01f)] private float fallSpeed = 9f;
    [SerializeField, Min(0f)] private float damage = 18f;
    [SerializeField] private FrozenEffect frozenEffect;//保证命中冰冻，仍由Health过滤无敌、格挡和死亡。
    private readonly HashSet<Health> touched = new();
    private AttackEffect[] hitEffects;
    private float groundY, landedAt;
    private bool landed;
    protected override void OnLaunched()
    {
        touched.Clear(); landed = false; landedAt = 0f;
        groundY = GroundAt(transform.position);
        hitEffects = frozenEffect != null ? new AttackEffect[] { frozenEffect } : null;
        Show(0);
    }
    protected override void Tick(float elapsed)
    {
        if (Count < 10) { StopEffect(); return; }
        if (landed)
        {
            int frame = 6 + Mathf.FloorToInt((Time.time - landedAt) * Fps);
            if (frame >= Count) StopEffect(); else Show(frame);
            return;
        }
        int fallingFrame = Mathf.Min(5, Mathf.FloorToInt(elapsed * Fps));
        Area(fallingFrame, out Bounds before);
        Vector3 position = transform.position;
        position.y = Mathf.Max(groundY, position.y - Mathf.Max(0.01f, fallSpeed) * Time.deltaTime);
        transform.position = position; Show(fallingFrame);
        if (Area(fallingFrame, out Bounds area))
        {
            area.Encapsulate(before);
            if (TryPlayer(out _, out Health health, out Collider2D body) && area.Intersects(body.bounds) && touched.Add(health))
            {
                float blood = health.CurBlood;
                health.TakeDamage(new DamageInfo(owner.transform, Mathf.Max(0f, damage) * damageScale, false,
                    effects: hitEffects, kind: DamageKind.Skill));
                hit |= health.CurBlood < blood;
            }
        }
        if (position.y <= groundY)
        { landed = true; landedAt = Time.time; Show(6); }
    }
    protected override void OnStopped() => touched.Clear();
}
