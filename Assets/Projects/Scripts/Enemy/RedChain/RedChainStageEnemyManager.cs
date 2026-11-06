using UnityEngine;

//第 5 关已接入广目天王，是否进入 Boss 阶段统一由基类按 boss 是否配置判断。
public sealed class RedChainStageEnemyManager : StageEnemyManager
{
    public override bool ValidateConfiguration(out string error)
    {
        if (HasBoss) return base.ValidateConfiguration(out error);
        error = null;
        return true;
    }
    protected override void InitBoss() { if (HasBoss) base.InitBoss(); }
}
