using UnityEngine;

public sealed class RollingFurnaceController : EnemyController
{
    public enum FurnaceAction { None, RollWindup, Rolling, WallStun, RollRecovery, SlamWindup, SlamAir, SlamRecovery, Vent }
    [SerializeField] private SpriteData RollCharge, BounceSlam, ShellVent;
    private RollingFurnaceCombat combat;
    private RollingFurnaceMovement movement;
    private SpriteRenderer visual;
    private FurnaceAction action;
    private float began, direction, patrolOrigin;
    private bool rebound, jumped;
    private SpriteData hitPose;
    public FurnaceAction Action => action;
    protected override bool UsesAnger => false;
    protected override bool CanStartNormalAttack => false;
    protected override void Awake()
    {
        base.Awake(); combat = GetComponent<RollingFurnaceCombat>(); movement = GetComponent<RollingFurnaceMovement>();
        visual = SM.GetComponent<SpriteRenderer>();
        // Original art faces LEFT, whereas the common facing API assumes RIGHT.
        // A fixed negative Visual scale adapts it without changing shared facing semantics.
    }
    protected override void OnEnable()
    {
        base.OnEnable(); action = FurnaceAction.None; patrolOrigin = transform.position.x; movement.ResetMotion();
        EH.Died += Died;
    }
    private void Pose(SpriteData clip, int frame)
    {
        SM.Pause(); visual.sprite = clip.sprites[Mathf.Clamp(frame, 0, clip.sprites.Length - 1)];
    }
    private void Enter(FurnaceAction next) { action = next; began = Time.time; }
    public bool BeginRoll(float sign)
    {
        if (action != FurnaceAction.None || !combat.RollReady || EH.Dead) return false;
        direction = sign < 0f ? -1f : 1f; rebound = false; combat.BeginRoll();
        EH.SetSuperArmor(this, true); EH.AttackedEnd(); movement.Stop();
        SM.ChangeSpriteFace(direction > 0f); SM.ChangeState(RollCharge); state = EnemyState.Skill;
        Enter(FurnaceAction.RollWindup); return true;
    }
    public bool BeginSlam()
    {
        if (action != FurnaceAction.None || !combat.SlamReady || EH.Dead) return false;
        combat.BeginSlam(); jumped = false; movement.Stop();
        SM.ChangeState(BounceSlam); state = EnemyState.Skill; Enter(FurnaceAction.SlamWindup); return true;
    }
    protected override bool ProcessSpecialState()
    {
        if (movement.IsExternallyControlled || (EH.Status?.BlocksActions ?? false))
        { if (action != FurnaceAction.None) CancelAction(); return false; }
        if (combat.VentReady && movement.IsOnGroundNow() && action != FurnaceAction.Vent)
        {
            CancelAction(); combat.BeginVent(); EH.AttackedEnd(); movement.Stop();
            SM.ChangeState(ShellVent); state = EnemyState.Skill; Enter(FurnaceAction.Vent);
        }
        if (action == FurnaceAction.None)
        {
            if (TrainingPassive || EH.Attacked || state == EnemyState.Attacked && !SM.IsFinished || !movement.IsOnGroundNow()) return false;
            float distance = Mathf.Abs(movement.DeltaX);
            if (distance >= 3f && distance <= 8f && BeginRoll(movement.DeltaX)) return true;
            if (distance >= 2f && distance <= 4f && BeginSlam()) return true;
            if (distance < 2f) // Leave space for the telegraphed attacks rather than stacking on the player.
            {
                float walk = -Mathf.Sign(movement.DeltaX);
                if (walk == 0f) walk = transform.position.x > patrolOrigin ? -1f : 1f;
                SetState(EnemyState.Walk); SM.ChangeSpriteFace(walk > 0f); movement.WalkDirection(walk, EH.Slow); return true;
            }
            return false;
        }
        if (EH.Attacked && !EH.Boost && action != FurnaceAction.WallStun && action != FurnaceAction.Vent)
        { CancelAction(); return false; }
        float elapsed = Time.time - began;
        switch (action)
        {
            case FurnaceAction.RollWindup:
                movement.HoldStill(); Pose(RollCharge, (int)(elapsed * 16f));
                if (elapsed >= .5f) { movement.BeginRoll(direction); combat.OpenRoll(false); Enter(FurnaceAction.Rolling); }
                break;
            case FurnaceAction.Rolling:
                Pose(RollCharge, 8 + (int)(elapsed * 24f) % 4);
                int result = movement.StepRoll();
                if (result == 1) { combat.CloseBoxes(); EH.SetSuperArmor(this, false); Enter(FurnaceAction.WallStun); HoldHitPose(); }
                else if (result == 2) RecoverRoll();
                else combat.SampleRoll(rebound);
                break;
            case FurnaceAction.WallStun:
                movement.HoldStill(); EH.AttackedEnd(); HoldHitPose();
                if (elapsed >= 1f)
                {
                    if (!rebound && movement.Remaining > .001f)
                    {
                        rebound = true; direction = -direction; SM.ChangeSpriteFace(direction > 0f);
                        movement.Rebound(); EH.SetSuperArmor(this, true); combat.OpenRoll(true); Enter(FurnaceAction.Rolling);
                    }
                    else RecoverRoll();
                }
                break;
            case FurnaceAction.RollRecovery:
                Pose(RollCharge, 12 + (int)(elapsed * 16f)); if (elapsed >= .25f) Finish(); break;
            case FurnaceAction.SlamWindup:
                movement.Stop(); Pose(BounceSlam, (int)(elapsed * 16f));
                if (elapsed >= 5f / 16f) { movement.Jump(); Enter(FurnaceAction.SlamAir); }
                break;
            case FurnaceAction.SlamAir:
                // Hold the descending illustration until physical contact; never hit on a timer in mid-air.
                Pose(BounceSlam, Mathf.Min(10, 5 + (int)(elapsed * 10f)));
                if (elapsed > .08f && !movement.IsOnGroundNow()) jumped = true;
                if (jumped && movement.IsOnGroundNow())
                { movement.Stop(); combat.Land(transform.position); Enter(FurnaceAction.SlamRecovery); Pose(BounceSlam, 11); }
                else if (elapsed > 5f) CancelAction();
                break;
            case FurnaceAction.SlamRecovery:
                Pose(BounceSlam, 11 + (int)(elapsed * 16f)); if (elapsed >= 5f / 16f) Finish(); break;
            case FurnaceAction.Vent:
                movement.Stop(); EH.AttackedEnd(); combat.AdvanceVent(Time.time);
                Pose(ShellVent, combat.Vent == RollingFurnaceCombat.VentPhase.Closed ? Mathf.Min(11, (int)(elapsed * 16f)) : Mathf.Min(15, 12 + (int)((elapsed - 2f) * 16f)));
                if (combat.Vent == RollingFurnaceCombat.VentPhase.None) Finish();
                break;
        }
        return true;
    }
    private void HoldHitPose()
    {
        if (hitPose == null) // Reuse the configured Hit art, without touching the common hit-state machine.
        {
            SetState(EnemyState.Attacked); SM.Pause();
            hitPose = new SpriteData { sprites = new[] { visual.sprite }, fps = 1f };
            state = EnemyState.Skill;
        }
        Pose(hitPose, 0);
    }
    private void RecoverRoll()
    { movement.StopRoll(); combat.CloseBoxes(); EH.SetSuperArmor(this, false); Enter(FurnaceAction.RollRecovery); }
    private void Finish()
    { CancelAction(); SM.Resume(); SetState(EnemyState.Idle); }
    private void CancelAction()
    {
        action = FurnaceAction.None; combat.CloseBoxes(); combat.EndVent(); movement.StopRoll();
        EH.SetSuperArmor(this, false); SM.Resume();
        if (!EH.Dead && state == EnemyState.Skill) SetState(EnemyState.Idle);
    }
    protected override void SpecialAttackOver() { if (combat != null) CancelAction(); }
    private void Died(DamageInfo _) => CancelAction();
    protected override void OnDisable()
    { if (EH != null) EH.Died -= Died; if (combat != null) CancelAction(); base.OnDisable(); }
}
