//Boss共用敌人的移动、受击和攻击调度，但默认不启用小怪的怒气状态。
public class BossController : EnemyController
{
    protected override bool UsesAnger => false;
}
