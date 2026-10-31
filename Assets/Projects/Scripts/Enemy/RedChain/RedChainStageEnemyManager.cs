using UnityEngine;

//第五关尚无 WestKing 预制体时，只运行已交付的小怪池；配好 Boss 后自动沿用完整关卡流程。
public sealed class RedChainStageEnemyManager : StageEnemyManager
{
    public bool HasBoss => boss != null;
    public override bool ValidateConfiguration(out string error)
    {
        if (HasBoss) return base.ValidateConfiguration(out error);
        error = null;
        return true;
    }
    protected override void InitBoss() { if (HasBoss) base.InitBoss(); }
}
