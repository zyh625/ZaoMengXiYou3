using UnityEngine;

public class SouthKingController : BossController
{
    [SerializeField] private SpriteData AttackOne, AttackTwo, AttackThree, SkillOne, SkillTwo, Counter;//技能三直接出现独立冰盾，无本体专属图片。
    [SerializeField, Min(0)] private int thrustStartFrame = 5, thrustEndFrame = 10;//第6至10帧突刺并无敌，末端为不包含。
    private SouthKingCombat combat;
    private SouthKingMovement movement;
    private SouthKingCombat.KingSkill pendingSkill;
    private SpriteData counterWindup, counterGuard, counterRiposte, counterRecovery;//同一张识破表的四个播放片段。
    private bool meleeWindowSampled;
    private int previousMeleeFrame;
    private SpriteOutlineController outline;
    protected override void Awake()
    {
        base.Awake(); combat = GetComponent<SouthKingCombat>(); movement = GetComponent<SouthKingMovement>();
        outline = SM.GetComponent<SpriteOutlineController>();
        counterWindup = Segment(0, 4, false); counterGuard = Segment(3, 1, true);
        counterRiposte = Segment(4, 9, false); counterRecovery = Segment(13, 3, false);
    }
    private SpriteData Segment(int start, int count, bool loop)
    {
        if (Counter == null || Counter.sprites == null || Counter.sprites.Length < start + count) return null;
        SpriteData part = new() { sprites = new Sprite[count], loop = loop, fps = Counter.fps };
        System.Array.Copy(Counter.sprites, start, part.sprites, 0, count); return part;
    }
    protected override void OnEnable()
    {
        base.OnEnable(); pendingSkill = SouthKingCombat.KingSkill.None;
        combat.CounterRecognized += OnCounterRecognized; combat.FrostChanged += RefreshFrost;
        EH.Died += OnDied; RefreshFrost();
    }
    protected override bool CanStartNormalAttack => combat != null && combat.CanStartAttack;
    protected override bool SkillReady => combat != null && movement != null && state != EnemyState.Skill
        && state != EnemyState.Attacking && (state != EnemyState.Attacked || SM.IsFinished)
        && (movement.IsOnGroundNow() || EH.Status != null && EH.Status.IsMelting)
        && combat.GetReadySkill() != SouthKingCombat.KingSkill.None;
    protected override void SkillSet() => pendingSkill = combat.GetReadySkill();
    protected override void HandleSkill()
    {
        if (pendingSkill == SouthKingCombat.KingSkill.Counter && (counterWindup == null || counterGuard == null
            || counterRiposte == null || counterRecovery == null)) { SetState(EnemyState.Idle); return; }//识破必须拥有完整16帧，错误配置不能进入无法结束的守势。
        if (!combat.BeginSkill(pendingSkill, movement.DeltaX > 0f)) { SetState(EnemyState.Idle); return; }
        SM.ChangeSpriteFace(combat.AttackFaceRight);
        if (pendingSkill == SouthKingCombat.KingSkill.IceShield)
        {
            combat.CheckSkillFrame(0); combat.CancelAction(); SetState(EnemyState.Idle); return;
        }
        SpriteData animation = pendingSkill == SouthKingCombat.KingSkill.IceSpikes ? SkillOne
            : pendingSkill == SouthKingCombat.KingSkill.SwordFormation ? SkillTwo : counterWindup;
        if (animation == null || animation.sprites == null || animation.sprites.Length == 0)
        { combat.CancelAction(); SetState(EnemyState.Idle); return; }
        SM.ChangeState(animation);
    }
    protected override void HandleAttack(int attack)
    {
        combat.BeginMelee(); meleeWindowSampled = false; previousMeleeFrame = -1;
        SM.ChangeState(attack == 0 ? AttackOne : attack == 1 ? AttackTwo : AttackThree);
    }
    protected override bool ProcessSpecialState()
    {
        if (combat == null || !combat.CanAct) return false;
        CheckMeleeFrame(); return false;//普通控制、空中受击与攻击结束仍交给共用控制器。
    }
    private void CheckMeleeFrame()
    {
        if (state != EnemyState.Attacking || EC.Attack < 0 || !combat.CanAct) return;
        int frame = SM.CurFrame;
        if (EC.Attack == 0)
        {
            bool active = frame >= thrustStartFrame && frame < thrustEndFrame;
            if (active)
            {
                EH.GrantInvincibility(movement, 1f);//在造成伤害之前设置，当前这一击的反伤也必须遵守无敌。
                if (Time.inFixedTimeStep) movement.Thrust(EC.AttackFaceRight);
            }
            else { EH.RemoveInvincibility(movement); if (frame >= thrustEndFrame) movement.Stop(); }
        }
        bool crossedWindow = false;
        if (!meleeWindowSampled)
            for (int i = Mathf.Max(0, previousMeleeFrame + 1); i <= Mathf.Min(frame, 128); i++)
                if (EC.AttackOpen(i)) { crossedWindow = true; break; }
        if (EC.AttackOpen(frame) || crossedWindow)
        {
            meleeWindowSampled = true; EC.AttackBegin(); combat.SampleMelee();
        }
        if (EC.AttackShut(frame)) EC.AttackEnd();
        previousMeleeFrame = frame;
    }
    protected override void CheckSkill() => CheckActionFrame();
    private void CheckActionFrame()
    {
        if (combat == null || EH.Dead || state != EnemyState.Skill) return;
        if (!combat.CanAct || combat.Skill == SouthKingCombat.KingSkill.None)
        { combat.CancelAction(); SetState(EnemyState.Idle); return; }
        if (combat.Skill == SouthKingCombat.KingSkill.Counter)
        {
            if (combat.Counter == SouthKingCombat.CounterStage.Windup && SM.CurFrame >= 3)
            { combat.EnterCounterGuard(); SM.ChangeState(counterGuard); }
            else if (combat.GuardExpired)
            { combat.BeginCounterRecovery(); SM.ChangeState(counterRecovery); }
        }
        combat.CheckSkillFrame(SM.CurFrame);
    }
    protected override void SkillOver()
    {
        combat.CheckSkillFrame(SM.CurFrame);//跳到末尾也先兑现已完成的有效帧。
        if (combat.Skill == SouthKingCombat.KingSkill.Counter && combat.Counter == SouthKingCombat.CounterStage.Riposte)
        { combat.BeginCounterRecovery(); SM.ChangeState(counterRecovery); return; }
        combat.CancelAction(); SetState(EnemyState.Idle);
    }
    private void OnCounterRecognized()
    {
        movement.Stop(); EH.AttackedEnd();
        combat.ProtectCounter(counterRiposte.sprites.Length / Mathf.Max(1f, counterRiposte.fps) + Time.fixedDeltaTime * 2f);
        SM.ChangeSpriteFace(combat.AttackFaceRight); SM.ChangeState(counterRiposte);
    }
    protected override void SpecialAttackOver()
    {
        EH.RemoveInvincibility(movement); movement.Stop(); combat?.CancelAction();
    }
    private void LateUpdate()
    {
        if (combat == null || EH.Dead) return;
        CheckMeleeFrame(); CheckActionFrame();
    }
    private void RefreshFrost()
    {
        if (outline == null) return;
        outline.SetOutlineColor(new Color(0.15f, 0.65f, 1f, 1f));
        outline.SetOutline(combat != null && combat.FrostActive && !EH.Dead);
    }
    private void OnDied(DamageInfo damage) { EH.RemoveInvincibility(movement); if (outline != null) outline.SetOutline(false); }
    protected override void OnDisable()
    {
        if (combat != null) { combat.CounterRecognized -= OnCounterRecognized; combat.FrostChanged -= RefreshFrost; combat.CancelAction(); }
        if (EH != null) { EH.Died -= OnDied; EH.RemoveInvincibility(movement); }
        if (outline != null) outline.SetOutline(false);
        base.OnDisable();
    }
}
