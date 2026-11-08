using System.Collections.Generic;
using UnityEngine;

//Boss 连击保护：短时间内被打中的次数达到上限，就获得一段时间霸体（照常掉血，但不被击退、打断或眩晕），
//霸体期间身边不断涌出白色光粒，并有一圈白光扩散提示。霸体结束后经过一段冷却才重新开始计数。
//关卡在生成 Boss 时、训练场在生成带 [BOSS] 的敌人时自动挂上，不需要改预制体。粒子全部用代码生成。
public sealed class BossComboGuard : MonoBehaviour
{
    [SerializeField, Min(1)] private int hitsToTrigger = 8;          //多少次命中触发
    [SerializeField, Min(0.1f)] private float countWindow = 2.5f;    //只统计最近这么多秒内的命中
    [SerializeField, Min(0.1f)] private float armorDuration = 3f;    //霸体持续时间
    [SerializeField, Min(0f)] private float cooldown = 6f;           //霸体结束后多久才重新计数

    private Health health;
    private SpriteRenderer body;
    private readonly Queue<float> hits = new();
    private float armorUntil = -1f, readyAt;
    private float nextParticle;
    private FxInstance glow;

    public bool Active => armorUntil >= 0f;

    public static void Attach(GameObject boss)
    {
        if (boss != null && boss.GetComponent<BossComboGuard>() == null && boss.GetComponent<Health>() != null)
            boss.AddComponent<BossComboGuard>();
    }

    private void Awake()
    {
        health = GetComponent<Health>();
        body = GetComponentInChildren<SpriteRenderer>(true);
    }

    private void OnEnable()
    {
        if (health != null) { health.DamageReceived += OnHit; health.Died += OnDied; }
        hits.Clear();
        readyAt = 0f;
    }

    private void OnDisable()
    {
        if (health != null) { health.DamageReceived -= OnHit; health.Died -= OnDied; }
        EndArmor(false);
    }

    private void OnDied(DamageInfo damage) => EndArmor(false);

    private void OnHit(DamageInfo damage, float actual)
    {
        if (damage.IsPeriodic || damage.Amount <= 0f || Active || Time.time < readyAt || health == null || health.Dead) return;//中毒不算连击
        float now = Time.time;
        hits.Enqueue(now);
        while (hits.Count > 0 && now - hits.Peek() > countWindow) hits.Dequeue();
        if (hits.Count >= hitsToTrigger) BeginArmor();
    }

    private void BeginArmor()
    {
        hits.Clear();
        armorUntil = Time.time + armorDuration;
        health.SetSuperArmor(this, true);//同时结束正在播放的受击反应
        if (body == null) return;
        Bounds b = body.bounds;
        //一圈白光向外扩散 + 中心闪一下
        for (int i = 0; i < 2; i++)
        {
            FxSpec ring = Fx.Make(null, b.center, b.size.y * 0.6f, new Color(1f, 1f, 1f, 0.9f - 0.35f * i));
            ring.sprite = FxTextures.Ring; ring.life = 0.45f + 0.2f * i; ring.growTo = 3.2f + i; ring.fadeOut = 0.35f;
            Fx.SortLike(ref ring, body, 3);
            Fx.Spawn(ring);
        }
        FxSpec flash = Fx.Make(null, b.center, b.size.y * 1.3f, new Color(1f, 1f, 1f, 0.8f));
        flash.sprite = FxTextures.Glow; flash.life = 0.3f; flash.growFrom = 0.6f; flash.growTo = 1.2f; flash.fadeOut = 0.25f;
        Fx.SortLike(ref flash, body, 2);
        Fx.Spawn(flash);
        //霸体期间身后一层淡淡的白光
        FxSpec aura = Fx.Make(null, b.center - transform.position, b.size.y * 1.35f, new Color(1f, 1f, 1f, 0.32f));
        aura.sprite = FxTextures.Glow; aura.follow = transform; aura.loop = true; aura.fadeIn = 0.15f;
        Fx.SortLike(ref aura, body, -3);
        glow = Fx.Spawn(aura);
    }

    private void EndArmor(bool startCooldown)
    {
        if (!Active) return;
        armorUntil = -1f;
        if (health != null) health.SetSuperArmor(this, false);
        if (glow != null) glow.Stop(0.3f);
        glow = null;
        if (startCooldown) readyAt = Time.time + cooldown;
    }

    private void Update()
    {
        if (!Active) return;
        if (health == null || health.Dead) { EndArmor(false); return; }
        if (Time.time >= armorUntil) { EndArmor(true); return; }
        if (body == null || Time.time < nextParticle) return;
        nextParticle = Time.time + 0.035f;
        SpawnParticle(body.bounds);
    }

    //白色光粒：从身体轮廓附近冒出，向外、向上飘散并淡出；大小、速度、寿命都随机，看起来不呆板
    private void SpawnParticle(Bounds b)
    {
        float angle = Random.Range(0f, Mathf.PI * 2f);
        Vector2 dir = new(Mathf.Cos(angle), Mathf.Sin(angle));
        Vector3 at = b.center + new Vector3(dir.x * b.extents.x * Random.Range(0.7f, 1.1f), dir.y * b.extents.y * Random.Range(0.7f, 1.05f), 0f);
        float size = Random.Range(0.12f, 0.32f) * Mathf.Clamp(b.size.y / 2.5f, 0.8f, 1.8f);
        FxSpec p = Fx.Make(null, at, size, new Color(1f, 1f, 1f, Random.Range(0.75f, 1f)));
        p.sprite = Random.value < 0.7f ? FxTextures.Spark : FxTextures.Glow;
        p.life = Random.Range(0.45f, 0.8f);
        p.fadeIn = 0.06f; p.fadeOut = p.life * 0.6f;
        p.growFrom = 1f; p.growTo = Random.Range(0.3f, 0.6f);
        p.drift = dir * Random.Range(0.4f, 1.1f) + Vector2.up * Random.Range(0.6f, 1.4f);
        p.spin = Random.Range(-180f, 180f);
        Fx.SortLike(ref p, body, Random.value < 0.5f ? 2 : -2);//一部分在身前、一部分在身后，有层次
        Fx.Spawn(p);
    }
}
