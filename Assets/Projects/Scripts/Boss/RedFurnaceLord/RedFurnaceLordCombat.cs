using System;
using System.Collections.Generic;
using UnityEngine;
using zmxy.GamePlay;

public sealed class RedFurnaceLordCombat : EnemyCombat
{
    [Header("Baseline (setup copies these into Health / Rewards / Movement)")]
    [SerializeField] private float baseHealth = 1310f, experienceReward = 260f, moveSpeed = .9f;
    [SerializeField] private float attackInterval = 2.2f;
    [Header("Selection distances")]
    [SerializeField] private float smashPreferredMin = 2f, smashRange = 4f, rangedRange = 7f;
    [Header("PestleSmash")]
    [SerializeField] private float pestleDamage = 34f, pestleRadius = 2.6f, pestleCooldown = 6f;
    [SerializeField] private int pestleOpenFrame = 8, pestleCloseFrame = 11;
    [Header("FurnaceBreath")]
    [SerializeField] private float breathDamage = 8f, breathTick = .5f, breathDuration = 2f, breathRange = 5f, breathHalfAngle = 35f, breathCooldown = 9f;
    [SerializeField] private int breathOpenFrame = 6, breathCloseFrame = 12;
    [Header("ChainSweep")]
    [SerializeField] private float chainDamage = 24f, chainRange = 6f, chainPull = 2f, chainFrontOffset = 1f, chainCooldown = 8f;
    [SerializeField] private int chainOpenFrame = 7, chainCloseFrame = 11;
    [Header("FurnaceOpen")]
    [SerializeField] private float firstCoreThreshold = .7f, secondCoreThreshold = .4f, coreDuration = 6f, coreDamageMultiplier = 1.5f;
    [SerializeField] private int coreActiveFrame = 8;
    [SerializeField] private float auraDamage = 8f, auraTick = .5f, auraRadius = 3f, burstDamage = 35f, burstRadius = 4f;
    [Header("Geometry / VFX")]
    [SerializeField] private Vector2 muzzleOffset = new(.8f, 1.5f);
    [SerializeField] private float attackHeight = 2.6f, impactHeight = 1.4f, effectDuration = 1f;
    [SerializeField] private BoxCollider2D[] skillBoxes;
    [SerializeField] private RedFurnaceLordEffectPool pestleImpact, furnaceBreath, chainSweepArc, coreBurst;
    private readonly float[] readyAt = new float[3];
    private readonly HashSet<Health> meleeHits = new();
    private Health health;
    private RedFurnaceLordMovement movement;
    private PlayerManager players;
    private PlayerController fallback;
    private float nextAttack, coreElapsed, auraElapsed, breathElapsed, breathAccumulator;
    private int thresholds, pendingCores, nextRanged = 2;
    private bool released, coreActive, burstReleased;
    public event Action CoreChanged;
    public bool IsCoreOpen { get; private set; }
    public float CoreRemaining => IsCoreOpen ? Mathf.Max(0f, coreDuration - coreElapsed) : 0f;
    public float CoreDuration => coreDuration;
    public int CurrentSkill { get; private set; } = -1;
    public int CoreStarts { get; private set; }
    public int BurstCount { get; private set; }
    public int AuraTicks { get; private set; }
    public int BreathTicks { get; private set; }
    public float BaseHealth => baseHealth;
    public float ExperienceReward => experienceReward;
    public float MoveSpeed => moveSpeed;
    protected override bool UsesAnger => false;
    public bool ControlInterrupted => health.Status != null && health.Status.BlocksActions || movement.IsExternallyControlled;
    private bool CanAct => health != null && !health.Dead && !IsCoreOpen && !ControlInterrupted && (!health.Attacked || health.Boost);
    public PlayerController Target
    {
        get
        {
            if (players == null) players = FindAnyObjectByType<PlayerManager>();
            if (players != null && players.isActiveAndEnabled) return players.CurrentPlayer;
            if (fallback != null && fallback.isActiveAndEnabled && !fallback.GetComponent<Health>().Dead) return fallback;
            foreach (var p in FindObjectsByType<PlayerController>())
                if (p.isActiveAndEnabled && !p.GetComponent<Health>().Dead) { fallback = p; return p; }
            return null;
        }
    }
    public void BindTarget(PlayerController target) { fallback = target; players = null; movement.SetTarget(target.transform); }
    public int ReadyAttack
    {
        get
        {
            var target = Target;
            if (!CanAct || CurrentSkill >= 0 || pendingCores > 0 || Time.time < nextAttack || target == null) return -1;
            float distance = Mathf.Abs(target.transform.position.x - transform.position.x);
            if (distance >= smashPreferredMin && distance <= smashRange && Time.time >= readyAt[0]) return 0;
            if (distance < smashPreferredMin && Time.time >= readyAt[0]) return 0;
            if (distance > smashRange && distance <= rangedRange)
            {
                if (Time.time >= readyAt[nextRanged]) return nextRanged;
                int other = nextRanged == 1 ? 2 : 1;
                if (Time.time >= readyAt[other]) return other;
            }
            return -1;
        }
    }
    protected override void Awake() { base.Awake(); health = GetComponent<Health>(); movement = GetComponent<RedFurnaceLordMovement>(); }
    protected override void OnEnable()
    {
        base.OnEnable(); CurrentSkill = -1; thresholds = pendingCores = CoreStarts = BurstCount = AuraTicks = BreathTicks = 0;
        IsCoreOpen = coreActive = released = burstReleased = false; nextRanged = 2; nextAttack = Time.time;
        coreElapsed = auraElapsed = breathElapsed = breathAccumulator = 0f;
        for (int i = 0; i < 3; i++) readyAt[i] = Time.time;
        CloseBoxes(); health.RemoveDamageMultiplier(this); health.SetSuperArmor(this, false);
        health.DamageResolved += OnDamage; health.Died += OnDied; CoreChanged?.Invoke();
    }
    public bool BeginAttack(int skill)
    {
        if (skill < 0 || skill > 2 || !CanAct || CurrentSkill >= 0 || pendingCores > 0 || Target == null
            || Time.time < nextAttack || Time.time < readyAt[skill]) return false;
        CurrentSkill = skill; released = false; meleeHits.Clear(); breathElapsed = breathAccumulator = 0f;
        readyAt[skill] = Time.time + (skill == 0 ? pestleCooldown : skill == 1 ? breathCooldown : chainCooldown) * RebirthCooldownMultiplier;
        if (skill > 0) nextRanged = skill == 1 ? 2 : 1;
        AttackFaceRight = Target.transform.position.x >= transform.position.x;
        health.Status?.NotifyAttackStarted(); movement.Stop(); ConfigureBoxes(); return true;
    }
    private void ConfigureBoxes()
    {
        float sign = AttackFaceRight ? 1f : -1f;
        skillBoxes[0].size = new Vector2(pestleRadius * BalanceRuntime.Number(this, "RedFurnaceLordCombat.ConfigureBoxes.1", 2f), impactHeight);
        skillBoxes[0].offset = new Vector2(0f, impactHeight * BalanceRuntime.Number(this, "RedFurnaceLordCombat.ConfigureBoxes.2", 0.5f));
        skillBoxes[1].size = new Vector2(breathRange, attackHeight * BalanceRuntime.Number(this, "RedFurnaceLordCombat.ConfigureBoxes.3", 2f));
        skillBoxes[1].offset = new Vector2(sign * (Mathf.Abs(muzzleOffset.x) + breathRange * BalanceRuntime.Number(this, "RedFurnaceLordCombat.ConfigureBoxes.4", 0.5f)), muzzleOffset.y);
        skillBoxes[2].size = new Vector2(chainRange, attackHeight);
        skillBoxes[2].offset = new Vector2(sign * chainRange * BalanceRuntime.Number(this, "RedFurnaceLordCombat.ConfigureBoxes.5", 0.5f), attackHeight * BalanceRuntime.Number(this, "RedFurnaceLordCombat.ConfigureBoxes.6", 0.5f));
    }
    public void SampleFrame(int index)
    {
        if (CurrentSkill < 0) return;
        if (!CanAct) { CancelAttack(); return; }
        int frame = index + 1;
        int open = CurrentSkill == 0 ? pestleOpenFrame : CurrentSkill == 1 ? breathOpenFrame : chainOpenFrame;
        int close = CurrentSkill == 0 ? pestleCloseFrame : CurrentSkill == 1 ? breathCloseFrame : chainCloseFrame;
        if (!released && frame >= open)
        {
            released = true;
            if (CurrentSkill == 0) pestleImpact.Play(transform.position, pestleRadius * BalanceRuntime.Number(this, "RedFurnaceLordCombat.SampleFrame.1", 2f), AttackFaceRight, effectDuration);
            else if (CurrentSkill == 1) furnaceBreath.Play(Muzzle + Vector2.right * (AttackFaceRight ? 1 : -1) * breathRange * BalanceRuntime.Number(this, "RedFurnaceLordCombat.SampleFrame.2", 0.5f), breathRange, AttackFaceRight, breathDuration);
            else chainSweepArc.Play((Vector2)transform.position + new Vector2((AttackFaceRight ? 1 : -1) * chainRange * BalanceRuntime.Number(this, "RedFurnaceLordCombat.SampleFrame.3", 0.5f), attackHeight * BalanceRuntime.Number(this, "RedFurnaceLordCombat.SampleFrame.4", 0.5f)), chainRange, AttackFaceRight, effectDuration);
        }
        bool active = frame >= open && frame <= close && (CurrentSkill != 1 || breathElapsed < breathDuration);
        skillBoxes[CurrentSkill].gameObject.SetActive(active);
        if (active && CurrentSkill != 1) SampleMelee();
    }
    public bool HoldBreathPose(int index) => CurrentSkill == 1 && released && index + 1 >= breathCloseFrame && breathElapsed < breathDuration;
    private Vector2 Muzzle => (Vector2)transform.position + new Vector2((AttackFaceRight ? 1 : -1) * Mathf.Abs(muzzleOffset.x), muzzleOffset.y);
    private void SampleMelee()
    {
        Physics2D.SyncTransforms(); var box = skillBoxes[CurrentSkill];
        foreach (var c in Physics2D.OverlapBoxAll(box.bounds.center, box.bounds.size, 0f)) TryMeleeHit(CurrentSkill, c);
    }
    public void TryMeleeHit(int skill, Collider2D collider)
    {
        if (skill == 1 || skill != CurrentSkill || !released || !CanAct || !skillBoxes[skill].gameObject.activeSelf) return;
        var player = collider.GetComponentInParent<PlayerController>();
        if (player == null || !player.CanReceiveHit) return;
        var hp = player.GetComponent<Health>(); if (hp.Dead || hp.IsInvincible || meleeHits.Contains(hp)) return;
        Vector2 delta = player.transform.position - transform.position;
        if (skill == 0 && delta.sqrMagnitude > pestleRadius * pestleRadius) return;
        if (skill == 2 && (delta.x * (AttackFaceRight ? 1 : -1) < 0f || Mathf.Abs(delta.x) > chainRange)) return;
        meleeHits.Add(hp); float before = hp.CurBlood;
        hp.TakeDamage(new DamageInfo(transform, (skill == 0 ? pestleDamage : chainDamage) * DamageMultiplier, skill == 0, false, null, false, false, DamageKind.Skill));
        if (skill == 2 && hp.CurBlood < before && hp.CanReceiveControl(ControlStrength.Ordinary)) Pull(player);
    }
    private void Pull(PlayerController player)
    {
        float destination = transform.position.x + (AttackFaceRight ? 1 : -1) * chainFrontOffset;
        Vector2 from = player.transform.position;
        float dx = Mathf.Clamp(destination - from.x, -chainPull, chainPull);
        var body = player.GetComponent<Rigidbody2D>(); var shape = player.GetComponent<Collider2D>();
        // Sweep the body against solid terrain so the pull cannot teleport through walls.
        var filter = new ContactFilter2D { useTriggers = false }; filter.SetLayerMask(1 << 6);
        var hits = new RaycastHit2D[8]; int count = shape.Cast(Vector2.right * Mathf.Sign(dx), filter, hits, Mathf.Abs(dx));
        for (int i = 0; i < count; i++)
            if (hits[i].normal.x * Mathf.Sign(dx) < -BalanceRuntime.Number(this, "RedFurnaceLordCombat.Pull.1", 0.1f))
                dx = Mathf.Sign(dx) * Mathf.Min(Mathf.Abs(dx), Mathf.Max(0f, hits[i].distance - BalanceRuntime.Number(this, "RedFurnaceLordCombat.Pull.2", 0.02f)));
        Vector2 to = from + Vector2.right * dx;
        if (body != null) { body.position = to; body.linearVelocityX = 0f; } else player.transform.position = to;
    }
    public void CancelAttack()
    {
        if (CurrentSkill >= 0) nextAttack = Time.time + attackInterval * RebirthCooldownMultiplier;
        if (CurrentSkill == 1) furnaceBreath?.ReleaseAll();
        CloseBoxes(); CurrentSkill = -1; released = false; meleeHits.Clear();
    }
    private void CloseBoxes() { if (skillBoxes != null) foreach (var box in skillBoxes) if (box != null) box.gameObject.SetActive(false); }
    private void OnDamage(DamageInfo damage, float actual)
    {
        if (health.Dead || actual <= 0f) return;
        if (health.ratio <= firstCoreThreshold && (thresholds & 1) == 0) { thresholds |= 1; pendingCores++; }
        if (health.ratio <= secondCoreThreshold && (thresholds & 2) == 0) { thresholds |= 2; pendingCores++; }
        if (!IsCoreOpen && pendingCores > 0 && !ControlInterrupted) BeginCore();
    }
    private void BeginCore()
    {
        pendingCores--; CancelAttack(); movement.Stop(); health.AttackedEnd();
        IsCoreOpen = true; coreActive = burstReleased = false; coreElapsed = auraElapsed = 0f; CoreStarts++;
        health.SetDamageMultiplier(this, coreDamageMultiplier);
        health.SetSuperArmor(this, true); CoreChanged?.Invoke();
    }
    public void SampleCoreFrame(int index) { if (IsCoreOpen && index + 1 >= coreActiveFrame) coreActive = true; }
    public void EndCore(bool burst)
    {
        if (!IsCoreOpen) return;
        // Leave the phase before damage callbacks: interruption/timeout/death cannot release it twice.
        IsCoreOpen = false; coreActive = false; coreElapsed = auraElapsed = 0f;
        health.RemoveDamageMultiplier(this); health.SetSuperArmor(this, false);
        CloseBoxes(); nextAttack = Time.time + attackInterval * RebirthCooldownMultiplier;
        if (burst && !burstReleased && !health.Dead)
        {
            burstReleased = true; BurstCount++;
            coreBurst.Play((Vector2)transform.position + Vector2.up, burstRadius * BalanceRuntime.Number(this, "RedFurnaceLordCombat.EndCore.1", 2f), AttackFaceRight, effectDuration);
            DamageRadius(burstRadius, burstDamage, true);
        }
        CoreChanged?.Invoke();
    }
    private void Update()
    {
        if (health.Dead) return;
        if (IsCoreOpen)
        {
            if (ControlInterrupted) { EndCore(true); return; }
            float step = Mathf.Min(Time.deltaTime, Mathf.Max(0f, coreDuration - coreElapsed));
            coreElapsed += step; auraElapsed += step;
            if (coreActive) while (auraElapsed + BalanceRuntime.Number(this, "RedFurnaceLordCombat.Update.1", 0.0001f) >= Mathf.Max(BalanceRuntime.Number(this, "RedFurnaceLordCombat.Update.2", 0.01f), auraTick))
            { auraElapsed -= Mathf.Max(BalanceRuntime.Number(this, "RedFurnaceLordCombat.Update.3", 0.01f), auraTick); AuraTicks++; DamageRadius(auraRadius, auraDamage, false); }
            if (coreElapsed >= coreDuration) EndCore(true); else CoreChanged?.Invoke();
            return;
        }
        if (pendingCores > 0 && !ControlInterrupted) { BeginCore(); return; }
        if (CurrentSkill < 0) return;
        if (!CanAct) { CancelAttack(); return; }
        movement.Stop();
        if (CurrentSkill != 1 || !released) return;
        float elapsed = Mathf.Min(Time.deltaTime, Mathf.Max(0f, breathDuration - breathElapsed));
        breathElapsed += elapsed; breathAccumulator += elapsed;
        while (breathAccumulator + BalanceRuntime.Number(this, "RedFurnaceLordCombat.Update.4", 0.0001f) >= Mathf.Max(BalanceRuntime.Number(this, "RedFurnaceLordCombat.Update.5", 0.01f), breathTick))
        {
            breathAccumulator -= Mathf.Max(BalanceRuntime.Number(this, "RedFurnaceLordCombat.Update.6", 0.01f), breathTick); BreathTicks++;
            DamageCone();
        }
        if (breathElapsed >= breathDuration) skillBoxes[1].gameObject.SetActive(false);
    }
    private void DamageCone()
    {
        foreach (var player in FindObjectsByType<PlayerController>())
        {
            if (!player.CanReceiveHit) continue;
            var shape = player.GetComponent<Collider2D>();
            Vector2 delta = (Vector2)shape.bounds.center - Muzzle;
            if (delta.magnitude <= breathRange && Vector2.Angle(Vector2.right * (AttackFaceRight ? 1 : -1), delta) <= breathHalfAngle)
                player.GetComponent<Health>().TakeDamage(new DamageInfo(transform, breathDamage * DamageMultiplier, false, false, null, false, true, DamageKind.Skill));
        }
    }
    private void DamageRadius(float radius, float amount, bool knockback)
    {
        foreach (var player in FindObjectsByType<PlayerController>())
            if (player.CanReceiveHit && ((Vector2)(player.transform.position - transform.position)).sqrMagnitude <= radius * radius)
                player.GetComponent<Health>().TakeDamage(new DamageInfo(transform, amount * DamageMultiplier, knockback, false, null, false, !knockback, DamageKind.Skill));
    }
    private void OnDied(DamageInfo damage) { pendingCores = 0; CancelAttack(); EndCore(false); ReleaseEffects(); CoreChanged?.Invoke(); }
    private void ReleaseEffects() { pestleImpact?.ReleaseAll(); furnaceBreath?.ReleaseAll(); chainSweepArc?.ReleaseAll(); coreBurst?.ReleaseAll(); }
    private void OnDisable()
    {
        if (health != null)
        {
            health.DamageResolved -= OnDamage; health.Died -= OnDied;
            health.RemoveDamageMultiplier(this); health.SetSuperArmor(this, false);
        }
        CancelAttack(); IsCoreOpen = coreActive = false; pendingCores = 0; coreElapsed = auraElapsed = 0f;
        ReleaseEffects(); CoreChanged?.Invoke();
    }
}
