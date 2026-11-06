using UnityEngine;

public sealed class WuYingWangController : BossController, IEnemyArenaBounds
{
    [SerializeField] private SpriteData lunge, groundOrb, takeoff, fly, flyingOrb, flyingHit, flyingDeath;
    [SerializeField] private WuYingWangOrb orbPrefab;
    [SerializeField] private Transform groundMuzzle, flyingMuzzle;
    [SerializeField, Min(0f)] private float lungeRange = 5f, lungeSpeed = 9f, lungeCooldown = 6f;
    [SerializeField, Min(0f)] private float orbCooldown = 9f;
    [SerializeField, Min(0f)] private float flightHeight = 2.3f, flightSpeed = 4f;
    [SerializeField, Min(0f)] private float bobHeight = 0.35f, bobFrequency = 0.65f;
    private enum Cast { None, GroundOrb, Takeoff, FlyingOrb }
    private Cast cast;
    private Level1EnemyMovement movement;
    private WuYingWangCombat combat;
    private bool faceRight, fired;
    private float firstAttackAt, lastAttack, takeoffStarted, flightAltitude, lungeOrigin, lungeTarget;
    private float arenaLeft = float.NegativeInfinity, arenaRight = float.PositiveInfinity;

    protected override void Awake()
    {
        base.Awake();
        movement = GetComponent<Level1EnemyMovement>();
        combat = GetComponent<WuYingWangCombat>();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        cast = Cast.None;
        firstAttackAt = Time.time + 1f;
        lastAttack = float.NegativeInfinity;
    }

    public void SetArenaBounds(float left, float right)
    {
        arenaLeft = Mathf.Min(left, right);
        arenaRight = Mathf.Max(left, right);
    }

    protected override SpriteData ResolveStateAnimation(EnemyState next, SpriteData fallback) =>
        combat == null || !combat.IsFlying ? fallback : next switch
        {
            EnemyState.Idle or EnemyState.Walk => fly,
            EnemyState.Attacked => flyingHit,
            EnemyState.Die => flyingDeath,
            _ => fallback
        };

    protected override bool ProcessSpecialState()
    {
        if (EH.Status.BlocksActions) return false;
        if (movement.ProcessExternalControl())
        {
            EH.AttackedEnd();
            return true;
        }
        if (EH.Attacked)
        {
            if (cast != Cast.None)
            {
                cast = Cast.None;
                SetState(EnemyState.Idle);
            }
            return false;
        }
        if (state == EnemyState.Attacked && !SM.IsFinished) return false;

        if (EC.Attack >= 0)
        {
            if (!SM.IsFinished)
            {
                UpdateLunge();
                return true;
            }
            AttackOver(true);
        }
        if (cast != Cast.None)
        {
            UpdateCast();
            return true;
        }
        if (!movement.HasTarget || EH.Status.BlocksMovement)
        {
            movement.Stop();
            SetState(EnemyState.Idle);
            return true;
        }

        Vector2 delta = movement.TargetPosition - (Vector2)transform.position;
        faceRight = delta.x >= 0f;
        SM.ChangeSpriteFace(faceRight);
        if (combat.IsFlying)
        {
            if (Ready(orbCooldown)) StartCast(Cast.FlyingOrb);
            else
            {
                SetState(EnemyState.Walk);
                MoveInFlight(Mathf.Clamp(delta.x, -1f, 1f) * flightSpeed);
            }
            return true;
        }

        movement.CheckIsGround();
        if (!movement.IsGround)
        {
            movement.Stop();
            SetState(EnemyState.Idle);
            return true;
        }
        if (combat.Rage >= combat.MaxRage)
        {
            flightAltitude = transform.position.y + flightHeight;
            StartCast(Cast.Takeoff);
            return true;
        }

        float distance = Mathf.Abs(delta.x);
        EC.ChangeCheckBox(faceRight);
        EC.CheckAttack();
        if (Mathf.Abs(delta.y) <= 2.25f && distance <= lungeRange && Ready(lungeCooldown) && EC.AttackReady())
        {
            EC.HasAtatck(faceRight);
            lastAttack = Time.time;
            lungeOrigin = transform.position.x;
            lungeTarget = LimitX(lungeOrigin + (faceRight ? 1f : -1f) * Mathf.Max(0f, distance - 0.6f));
            SetState(EnemyState.Attacking);
        }
        else if ((distance > lungeRange || Mathf.Abs(delta.y) > 2.25f) && Ready(orbCooldown))
            StartCast(Cast.GroundOrb);
        else if (distance <= 1.1f || (faceRight ? transform.position.x >= arenaRight : transform.position.x <= arenaLeft))
        {
            movement.Stop();
            SetState(EnemyState.Idle);
        }
        else
        {
            movement.Move(EH.Slow);
            SetState(EnemyState.Walk);
        }
        return true;
    }

    private bool Ready(float cooldown) => Time.time >= firstAttackAt && Time.time >= lastAttack + cooldown * RebirthScaling.Current.AttackCooldown;
    private float LimitX(float x) => Mathf.Clamp(x, arenaLeft, arenaRight);

    private void UpdateLunge()
    {
        int frame = SM.CurFrame;
        if (EC.AttackOpen(frame)) EC.AttackBegin();
        if (EC.AttackShut(frame)) EC.AttackEnd();
        float destination = frame < 10 ? lungeTarget : lungeOrigin;
        movement.Fly(Vector2.right * Mathf.Clamp((destination - transform.position.x) / Time.fixedDeltaTime, -lungeSpeed, lungeSpeed));
    }

    private void StartCast(Cast next)
    {
        cast = next;
        fired = false;
        movement.Stop();
        if (next == Cast.Takeoff) takeoffStarted = Time.time;
        else
        {
            lastAttack = Time.time;
            EH.Status.NotifyAttackStarted();
        }
        SetState(EnemyState.Skill);
    }

    protected override void HandleAttack(int attack) => SM.ChangeState(lunge);

    protected override void HandleSkill() => SM.ChangeState(cast switch
    {
        Cast.Takeoff => takeoff,
        Cast.FlyingOrb => flyingOrb,
        _ => groundOrb
    });

    private void UpdateCast()
    {
        int frame = SM.CurFrame;
        if (cast == Cast.Takeoff)
        {
            // Ascend during the transformation, then hold the attained altitude.
            float elapsed = Time.time - takeoffStarted;
            float speed = elapsed >= 0.15f ? Mathf.Clamp((flightAltitude - transform.position.y) / Time.fixedDeltaTime, 0f, 7f) : 0f;
            movement.Fly(Vector2.up * speed);
        }
        else
        {
            if (cast == Cast.FlyingOrb) MoveInFlight(0f);
            if (frame >= 10 && !fired && movement.HasTarget)
            {
                fired = true;
                Transform muzzle = cast == Cast.FlyingOrb ? flyingMuzzle : groundMuzzle;
                Vector2 position = transform.TransformPoint(new Vector3(Mathf.Abs(muzzle.localPosition.x) * (faceRight ? 1f : -1f), muzzle.localPosition.y));
                Vector2 aim = movement.TargetPosition + Vector2.up * 0.7f - position;
                Instantiate(orbPrefab, position, Quaternion.identity).Launch(combat, aim, arenaLeft, arenaRight);
            }
        }
        if (!SM.IsFinished) return;
        if (cast == Cast.Takeoff)
        {
            flightAltitude = transform.position.y;
            combat.EnterFlight();
        }
        cast = Cast.None;
        SetState(EnemyState.Idle);
    }

    private void MoveInFlight(float x)
    {
        float y = flightAltitude + Mathf.Sin(Time.time * bobFrequency * 2f * Mathf.PI) * bobHeight;
        float horizontal = Mathf.Clamp((LimitX(transform.position.x + x * Time.fixedDeltaTime) - transform.position.x)
            / Time.fixedDeltaTime, -flightSpeed, flightSpeed);
        float vertical = Mathf.Clamp((y - transform.position.y) * 5f, -flightSpeed, flightSpeed);
        movement.Fly(new Vector2(horizontal, vertical) * (EH.Slow ? 0.5f : 1f));
    }

    protected override void SpecialAttackOver() => cast = Cast.None;
}
