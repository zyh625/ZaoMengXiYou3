using UnityEngine;

public class BlueWheelController : EnemyController
{
    [SerializeField] private SpriteData SingleThrow, SingleBack, DoubleThrow, DoubleBack, Hide;//其余普通状态继续使用基类数据。
    private BlueWheelCombat combat;
    private BlueMovement movement;
    private bool pendingHide;//查询起手不提前消耗冷却。
    private SpriteData hiddenLoop;//保留Hide最后四帧，包括透明帧。
    protected override void Awake()
    {
        base.Awake();
        combat = GetComponent<BlueWheelCombat>();
        movement = GetComponent<BlueMovement>();
        BuildHiddenLoop();
    }
    private void BuildHiddenLoop()
    {
        if (Hide == null || Hide.sprites == null || Hide.sprites.Length <= 12) return;
        int first = Mathf.Clamp(combat != null ? combat.HideStartFrame : 12, 0, Hide.sprites.Length - 1);
        int count = Hide.sprites.Length - first;
        hiddenLoop = new SpriteData { sprites = new Sprite[count], loop = true,
            fps = count / (combat != null ? combat.ShadowInterval : BalanceRuntime.Number(this, "BlueWheelController.BuildHiddenLoop.1", 1.5f)) };
        System.Array.Copy(Hide.sprites, first, hiddenLoop.sprites, 0, count);
    }
    protected override bool SkillReady => combat != null && movement != null && state != EnemyState.Skill
        && (state != EnemyState.Attacked || SM.IsFinished) && movement.IsOnGroundNow()
        && (combat.CanHide || !combat.IsHidden && combat.CanThrow);
    protected override void SkillSet() => pendingHide = combat.CanHide;
    protected override void HandleSkill()
    {
        if (pendingHide)
        {
            if (!HasFrames(Hide) || hiddenLoop == null || !combat.BeginHide()) { SetState(EnemyState.Idle); return; }
            SM.ChangeState(Hide);
        }
        else
        {
            if (!HasFrames(SingleThrow) || !HasFrames(SingleBack) || !combat.BeginThrow(movement.DeltaX > 0f))
            { SetState(EnemyState.Idle); return; }
            SM.ChangeSpriteFace(combat.AttackFaceRight);
            SM.ChangeState(combat.DoubleThrow && HasFrames(DoubleThrow) ? DoubleThrow : SingleThrow);
        }
    }
    private static bool HasFrames(SpriteData data) => data != null && data.sprites != null && data.sprites.Length > 0;
    protected override void CheckSkill() => CheckActionFrame();
    private void CheckActionFrame()//LateUpdate和物理更新只执行同一门闩一次。
    {
        if (combat == null || EH.Dead) return;
        if (state != EnemyState.Skill)
        {
            if (combat.Action != BlueWheelCombat.WheelAction.None || combat.IsHidden) combat.InterruptAction();
            return;
        }
        if (!combat.CanAct)
        {
            combat.InterruptAction();
            SetState(EnemyState.Idle);
            return;
        }
        switch (combat.Action)
        {
            case BlueWheelCombat.WheelAction.Throw: combat.CheckReleaseFrame(SM.CurFrame); break;
            case BlueWheelCombat.WheelAction.Return: combat.CheckCatchFrame(SM.CurFrame); break;
            case BlueWheelCombat.WheelAction.HideWindup:
                if (SM.CurFrame >= combat.HideStartFrame)
                {
                    combat.EnterHidden();
                    if (combat.IsHidden) SM.ChangeState(hiddenLoop);
                }
                break;
            default:
                if (!combat.IsHidden) SetState(EnemyState.Idle);
                break;
        }
    }
    protected override void SkillOver()
    {
        if (combat.Action == BlueWheelCombat.WheelAction.Throw)
        {
            SpriteData returning = combat.DoubleThrow && HasFrames(DoubleBack) ? DoubleBack : SingleBack;
            combat.BeginReturn(returning != null ? returning.fps : BalanceRuntime.Number(this, "BlueWheelController.SkillOver.1", 16f));
            SM.ChangeState(returning);//基类保留新启动的收回动作，不在同帧覆盖为行走。
        }
        else
        {
            combat.CompleteReturn();
            SetState(EnemyState.Idle);
        }
    }
    protected override bool ProcessSpecialState()//不抢死亡、冻结、吸附和受击的优先级。
    {
        if (combat == null) return false;
        if (!combat.IsHidden) return KeepDistance();
        if (!combat.CanAct || state != EnemyState.Skill)
        {
            combat.InterruptAction();
            return false;
        }
        if (EC.AngryRequest || EC.Angry && EC.AngryEnd) return false;//仍让共用怒气分支处理强化起止。
        movement.CheckIsGround();
        if (!movement.IsGround) { movement.Stop(); return true; }
        combat.TryHiddenThrow();
        movement.PatrolHidden(EH.Slow);
        if (movement.PatrolDirection != 0f) SM.ChangeSpriteFace(movement.PatrolDirection > 0f);
        return true;
    }
    //远程怪：只在通用流程本来会“走路或站立”时接管移动，改为与玩家保持距离；
    //冻结、吸附、怒气、技能、受击、空中仍按通用流程的原有优先级处理。
    private bool KeepDistance()
    {
        if (movement == null || !movement.HasTarget) return false;
        if (EH.Status != null && (EH.Status.BlocksActions || EH.Status.BlocksMovement)) return false;
        if (movement.IsExternallyControlled) return false;
        if (UsesAnger && (EC.AngryRequest || EC.Angry && EC.AngryEnd)) return false;
        if (state == EnemyState.Skill || SkillReady) return false;
        if (EH.Attacked || state == EnemyState.Attacked && !SM.IsFinished) return false;
        if (EC.Attack != -1) return false;
        movement.CheckIsGround();
        if (!movement.IsGround) return false;

        movement.KeepDistance(EH.Slow);
        float direction = movement.KeepDistanceDirection;
        SM.ChangeSpriteFace(direction != 0f ? direction > 0f : movement.DeltaX > 0f);//走动时面向前进方向，站定时面向玩家
        SetState(direction != 0f ? EnemyState.Walk : EnemyState.Idle);
        return true;
    }

    protected override void SpecialAttackOver() => combat?.InterruptAction();//死亡和受控时已出手飞轮不再回手。
    private void LateUpdate() => CheckActionFrame();
    protected override void OnDisable()
    {
        combat?.InterruptAction();
        base.OnDisable();
    }
}
