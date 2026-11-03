//第六关小怪演示沿用StageEnemyManager；以后填入Boss便自动使用标准Boss初始化。
public sealed class GreenHammerStageEnemyManager : StageEnemyManager
{
    public bool HasBoss=>boss!=null;
    public override bool ValidateConfiguration(out string error)
    {
        if(HasBoss)return base.ValidateConfiguration(out error);
        error=null;return true;
    }
    protected override void InitBoss(){if(HasBoss)base.InitBoss();}
}
