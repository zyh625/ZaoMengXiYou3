using UnityEngine;

public sealed class RedBoomController : EnemyController
{
    [SerializeField] private SpriteData AttackOne, AttackTwo, AttackThree;
    private RedBoomCombat combat;
    private RedBoomMovement movement;
    private float resumeAt;
    private bool waiting;
    protected override bool UsesAnger => false;
    protected override bool CanStartNormalAttack => false;

    protected override void Awake()
    {
        base.Awake();
        combat = GetComponent<RedBoomCombat>();
        movement = GetComponent<RedBoomMovement>();
    }

    protected override bool ProcessSpecialState()
    {
        if (EH.Attacked || movement.IsExternallyControlled || EH.Status != null && EH.Status.BlocksActions
            || state == EnemyState.Attacked && !SM.IsFinished) return false;
        PlayerController target = combat.Target;
        if (target == null) { combat.CancelCast(); movement.Stop(); SetState(EnemyState.Idle); return true; }
        movement.CheckIsGround();
        if (!movement.IsGround) return false;
        float delta = target.transform.position.x - transform.position.x;
        if (Mathf.Abs(delta) < 3f && movement.TryBlink(delta))
        {
            combat.CancelCast();
            SetState(EnemyState.Idle);
            SM.ChangeSpriteFace(target.transform.position.x > transform.position.x);
            return true;
        }
        if (state == EnemyState.Skill) return false;
        SM.ChangeSpriteFace(delta > 0f);
        if (EH.Status != null && EH.Status.BlocksMovement)
        {
            if (Mathf.Abs(delta) >= 4f && Mathf.Abs(delta) <= 7f && combat.ReadySkill >= 0) return false;
            movement.Stop(); SetState(EnemyState.Idle); return true;
        }
        if (Mathf.Abs(delta) >= 4f && Mathf.Abs(delta) <= 7f)
        {
            if (combat.ReadySkill >= 0) return false;
            movement.Stop(); SetState(EnemyState.Idle); return true;
        }
        SetState(EnemyState.Walk);
        movement.MoveInRange(delta, Mathf.Abs(delta) < 4f, EH.Slow);
        return true;
    }

    protected override bool SkillReady => state != EnemyState.Skill && combat != null && combat.ReadySkill >= 0
        && (state != EnemyState.Attacked || SM.IsFinished) && !EH.Attacked && combat.InRange;
    protected override void SkillSet() => combat.BeginCast(combat.ReadySkill);
    protected override void HandleSkill()
    {
        int skill = combat.CurrentSkill;
        SpriteData clip = skill == 0 ? AttackOne : skill == 1 ? AttackTwo : AttackThree;
        SM.ChangeSpriteFace(combat.FaceRight);
        SM.ChangeState(clip);
        float hold = Mathf.Max(0f, combat.Windup - combat.ReleaseFrame / Mathf.Max(1f, clip.fps));
        resumeAt = Time.time + hold;
        waiting = hold > 0f;
        if (waiting) SM.Pause();
    }

    protected override void CheckSkill()
    {
        if (state != EnemyState.Skill) return;
        if (EH.Attacked || EH.Dead || movement.IsExternallyControlled || EH.Status != null && EH.Status.BlocksActions)
        { combat.CancelCast(); SetState(EnemyState.Idle); return; }
        if (waiting)
        {
            if (Time.time < resumeAt) return;
            waiting = false; SM.Resume();
        }
        combat.SampleFrame(SM.CurFrame);
    }

    protected override void SpecialAttackOver() { if (state == EnemyState.Skill) combat?.CancelCast(); }
    protected override void SkillOver() { combat.CancelCast(); movement.Stop(); SetState(EnemyState.Idle); }
    private void LateUpdate() { if (state == EnemyState.Skill) CheckSkill(); }
    protected override void OnDisable() { combat?.CancelCast(); waiting = false; base.OnDisable(); }
}
