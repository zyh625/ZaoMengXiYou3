using System.Collections.Generic;
using UnityEngine;

public sealed class RedBoomCombat : EnemyCombat
{
    [SerializeField] private RedBoomLantern lanternPrefab;
    [SerializeField] private RedBoomRay rayPrefab;
    [SerializeField] private RedBoomMark markPrefab;
    [SerializeField] private Transform muzzle;
    [SerializeField, Min(1f)] private float rayRange = 10f;
    [SerializeField, Min(0.05f)] private float rayHeight = 0.55f;
    [SerializeField] private float[] skillCooldowns = { 5f, 7f, 12f };
    private static readonly float[] windups = { 0.9f, 0.5f, 0.6f };
    private static readonly int[] releaseFrames = { 12, 8, 9 };
    private readonly float[] readyAt = new float[3];
    private readonly Collider2D[] hits = new Collider2D[32];
    private readonly HashSet<Health> hitHealths = new(16);
    private Health health;
    private PlayerManager party;
    private PlayerController fallback;
    private float resolveAt, castAt;
    private bool released;
    private Vector2 rayFrom, rayTo;
    private RedBoomRay warning;
    public int CurrentSkill { get; private set; } = -1;
    public bool FaceRight { get; private set; }
    public int RayShots { get; private set; }
    public int ReleaseFrame => CurrentSkill >= 0 ? releaseFrames[CurrentSkill] : 0;
    public float Windup => CurrentSkill >= 0 ? windups[CurrentSkill] : 0f;
    public RedBoomRay Warning => warning;
    protected override bool UsesAnger => false;

    public PlayerController Target
    {
        get
        {
            if (party != null && party.isActiveAndEnabled) return party.CurrentPlayer;
            if (fallback != null && fallback.isActiveAndEnabled && !fallback.GetComponent<Health>().Dead) return fallback;
            if (Time.time < resolveAt) return null;
            resolveAt = Time.time + 0.5f;
            party = FindAnyObjectByType<PlayerManager>();
            if (party != null && party.isActiveAndEnabled) return party.CurrentPlayer;
            foreach (PlayerController player in FindObjectsByType<PlayerController>())
                if (player.isActiveAndEnabled && !player.GetComponent<Health>().Dead) { fallback = player; return player; }
            return null;
        }
    }
    public bool InRange => Target != null && Mathf.Abs(Target.transform.position.x - transform.position.x) is >= 4f and <= 7f;
    public int ReadySkill
    {
        get
        {
            PlayerController target = Target;
            if (CurrentSkill >= 0 || health == null || health.Dead || health.Attacked || target == null
                || target.GetComponent<Health>().Dead || health.Status != null && health.Status.BlocksActions) return -1;
            if (Time.time >= readyAt[2] && !RedBoomMark.IsMarked(target.GetComponent<Health>())) return 2;
            if (Time.time >= readyAt[0]) return 0;
            if (Time.time >= readyAt[1]) return 1;
            return -1;
        }
    }

    protected override void Awake() { base.Awake(); health = GetComponent<Health>(); party = FindAnyObjectByType<PlayerManager>(); }
    protected override void OnEnable()
    {
        base.OnEnable();
        if (health == null) health = GetComponent<Health>();
        CurrentSkill = -1; RayShots = 0; released = false;
        for (int i = 0; i < 3; i++) readyAt[i] = Time.time;
        health.Died += OnDied;
    }
    public void BindTarget(PlayerController target) { fallback = target; party = null; }
    private Vector2 Origin => (Vector2)transform.position + new Vector2((FaceRight ? 1f : -1f) * Mathf.Abs(muzzle.localPosition.x), muzzle.localPosition.y);

    public bool BeginCast(int skill)
    {
        PlayerController target = Target;
        if (skill < 0 || skill >= 3 || target == null || health == null || health.Dead || health.Attacked
            || CurrentSkill >= 0 || Time.time < readyAt[skill] || health.Status != null && health.Status.BlocksActions) return false;
        CurrentSkill = skill;
        readyAt[skill] = Time.time + skillCooldowns[skill];
        castAt = Time.time; released = false;
        FaceRight = target.transform.position.x >= transform.position.x;
        health.Status?.NotifyAttackStarted();
        if (skill == 0)
        {
            rayFrom = Origin;
            Vector2 direction = FaceRight ? Vector2.right : Vector2.left;
            rayTo = rayFrom + direction * rayRange;
            RaycastHit2D wall = Physics2D.Raycast(rayFrom, direction, rayRange, 1 << 6);
            if (wall.collider != null) rayTo = wall.point - direction * 0.02f;
            warning = RedBoomEffectPool.GetOrCreate().Warn(rayPrefab, rayFrom, rayTo, GetComponentInChildren<SpriteRenderer>());
        }
        return true;
    }

    public void SampleFrame(int frame)
    {
        if (CurrentSkill < 0 || released) return;
        if (health == null || health.Dead || health.Attacked || health.Status != null && health.Status.BlocksActions)
        { CancelCast(); return; }
        if (frame < ReleaseFrame || Time.time + 0.0001f < castAt + Windup) return;
        released = true;
        if (CurrentSkill == 0)
        {
            RayShots++;
            warning?.Flash();
            Vector2 size = new(Mathf.Abs(rayTo.x - rayFrom.x), rayHeight);
            ContactFilter2D filter = RedBoomHitUtility.PlayerFilter;
            int count = Physics2D.OverlapBox((rayFrom + rayTo) / 2f, size, 0f, filter, hits);
            hitHealths.Clear();
            float scale = DamageMultiplier;
            for (int i = 0; i < count; i++)
            {
                Health victim = RedBoomHitUtility.PlayerHealth(hits[i]);
                if (victim == null || !hitHealths.Add(victim)) continue;
                float before = victim.CurBlood;
                victim.TakeDamage(new DamageInfo(transform, 12f * scale, false));
                if (victim.CurBlood < before && !victim.Dead) RedBoomBurn.Apply(victim, transform, scale);
            }
        }
        else if (CurrentSkill == 1)
        {
            PlayerController target = Target;
            if (target != null) RedBoomEffectPool.GetOrCreate().Throw(lanternPrefab, this, Origin, target);
        }
        else
        {
            PlayerController target = Target;
            if (target != null) RedBoomEffectPool.GetOrCreate().Mark(markPrefab, target.GetComponent<Health>(), GetComponentInChildren<SpriteRenderer>());
        }
    }

    public void CancelCast()
    {
        if (!released && warning != null) warning.Cancel();
        warning = null; CurrentSkill = -1; released = false;
    }
    private void OnDied(DamageInfo damage) => CancelCast();
    private void OnDisable() { if (health != null) health.Died -= OnDied; CancelCast(); }
}
