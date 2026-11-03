// Attackable auxiliary target, with no AI, rewards or wave registration.
public sealed class LateStageProxyController : EnemyController
{
    protected override void Awake(){base.Awake();}
    protected override void OnEnable(){enabled=false;}
    protected override void OnDisable(){}
}
