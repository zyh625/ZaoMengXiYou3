using UnityEngine;

//鸣曜星鸮：和第一关鸟怪一样在玩家头顶区域随机盘旋；技能就绪时停下施法。
public sealed class ResonantStarOwlController : EnemyController
{
    [SerializeField] private SpriteData SoundRings, HighLowChime, ResonanceBand;
    [Header("盘旋")]
    [SerializeField, Min(0f)] private float flySpeed = 3f;//盘旋时的最大飞行速度
    [SerializeField, Min(0.1f)] private float steering = 5f;//转向加速度，越小轨迹越圆滑
    [SerializeField, Min(0f)] private float hoverHalfWidth = 3.5f;//盘旋区域相对玩家的左右半宽
    [SerializeField] private Vector2 hoverHeight = new(2.6f, 3.8f);//盘旋区域高于玩家身体中心的范围
    [SerializeField] private Vector2 retargetTime = new(1.4f, 2.8f);//多久换一个盘旋点
    private Vector2 hoverOffset, hoverVelocity;
    private float nextRetarget;
    private ResonantStarOwlCombat combat;
    private ResonantStarOwlMovement movement;
    private SpriteRenderer visual;
    protected override bool UsesAnger => false;
    protected override bool CanStartNormalAttack => false;
    protected override void Awake()
    {
        base.Awake(); combat = GetComponent<ResonantStarOwlCombat>();
        movement = GetComponent<ResonantStarOwlMovement>(); visual = SM.GetComponent<SpriteRenderer>();
    }
    protected override void OnEnable()
    {
        base.OnEnable();
        hoverVelocity = Vector2.zero;
        PickHoverPoint();
    }
    protected override bool ProcessSpecialState()
    {
        movement.StepFlight();
        if (EH.Attacked || movement.IsExternallyControlled || EH.Status != null && EH.Status.BlocksActions
            || state == EnemyState.Attacked && !SM.IsFinished) return false;
        if (state == EnemyState.Skill) return false;
        if (movement.Falling) { movement.Stop(); hoverVelocity = Vector2.zero; SetState(EnemyState.Idle); return true; }
        var target = combat.Target;
        if (target == null) { Brake(); SetState(EnemyState.Idle); return true; }
        Vector2 player = Center(target);
        SM.ChangeSpriteFace(player.x >= transform.position.x);
        if (combat.ReadySkill >= 0) return false;
        Hover(player);
        SetState(EnemyState.Walk); return true;
    }
    private static Vector2 Center(PlayerController player)
    {
        var body = player.GetComponent<Collider2D>();
        return body != null ? (Vector2)body.bounds.center : (Vector2)player.transform.position;
    }
    private void Hover(Vector2 player)
    {
        Vector2 toPoint = player + hoverOffset - (Vector2)transform.position;
        if (Time.time >= nextRetarget || toPoint.sqrMagnitude < 0.09f) PickHoverPoint();
        float speed = flySpeed * RebirthScaling.Current.Movement * (EH.Slow ? 0.5f : 1f);
        Vector2 desired = Vector2.ClampMagnitude(toPoint * 1.5f, speed);//靠近目标点时自然减速
        hoverVelocity = Vector2.MoveTowards(hoverVelocity, desired, steering * Time.fixedDeltaTime);
        movement.Fly(hoverVelocity);
    }
    private void Brake()//施法或无目标时平滑减速到悬停
    {
        hoverVelocity = Vector2.MoveTowards(hoverVelocity, Vector2.zero, steering * 2f * Time.fixedDeltaTime);
        movement.Fly(hoverVelocity);
    }
    private void PickHoverPoint()
    {
        hoverOffset = new Vector2(Random.Range(-hoverHalfWidth, hoverHalfWidth), Random.Range(hoverHeight.x, hoverHeight.y));
        nextRetarget = Time.time + Random.Range(retargetTime.x, retargetTime.y);
    }
    protected override bool SkillReady => state != EnemyState.Skill && combat.ReadySkill >= 0
        && !movement.Falling && !movement.IsExternallyControlled && !EH.Attacked
        && (state != EnemyState.Attacked || SM.IsFinished);
    protected override void SkillSet() => combat.BeginCast(combat.ReadySkill);
    protected override void HandleSkill()
    {
        SM.ChangeSpriteFace(combat.FaceRight);
        SM.ChangeState(Clip); SM.Pause();
    }
    private SpriteData Clip => combat.CurrentSkill == 0 ? SoundRings : combat.CurrentSkill == 1 ? HighLowChime : ResonanceBand;
    protected override void CheckSkill()
    {
        if (state != EnemyState.Skill) return;
        if (EH.Attacked || EH.Dead || movement.IsExternallyControlled || EH.Status != null && EH.Status.BlocksActions)
        { combat.CancelCast(); SetState(EnemyState.Idle); return; }
        Brake();
        if (combat.CurrentSkill < 0) { SetState(EnemyState.Idle); return; }
        visual.sprite = Clip.sprites[combat.AnimationFrame];
        combat.SampleCast();
        if (combat.Finished) SkillOver();
    }
    protected override void SpecialAttackOver() { if (state == EnemyState.Skill) combat.CancelCast(); }
    protected override void SkillOver() { combat.FinishCast(); SetState(EnemyState.Idle); }
    private void LateUpdate()
    {
        if (attackedBox != null) attackedBox.enabled = true;
        if (state == EnemyState.Skill) CheckSkill();
    }
    protected override void OnDisable() { combat?.CancelCast(); base.OnDisable(); }
}
