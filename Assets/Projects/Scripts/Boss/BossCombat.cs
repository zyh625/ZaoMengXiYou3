//Boss默认不继承小怪的自动怒气，特殊状态由具体Boss的Combat子类管理。
public class BossCombat : EnemyCombat
{
    protected override bool UsesAnger => false;
}
