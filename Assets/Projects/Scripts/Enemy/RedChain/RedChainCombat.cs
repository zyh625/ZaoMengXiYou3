using UnityEngine;

public sealed class RedChainCombat : EnemyCombat
{
    [SerializeField] private RedChainAttackBox[] hitBoxes;
    [SerializeField] private RedChainShockWaveEmitter shockWave;
    private bool sampled;
    private SpriteRenderer visual;
    private const int ChainAttack = 1;//向前甩链再收回
[SerializeField]private int[] openFrames = { 6, 8, 8 };
[SerializeField]private float[] windups = { 0.6f, 0.8f, 0.9f };
    protected override bool UsesAnger => false;
    public int OpenFrame => Attack >= 0 ? openFrames[Attack] : 0;
    public float Windup => Attack >= 0 ? windups[Attack] : 0f;

    public bool SelectRequest(float deltaX)
    {
        if (AttackRequest == null || AttackRequest.Length != 3) return false;
        int chosen = -1;
        float distance = Mathf.Abs(deltaX);
        if (distance > BalanceRuntime.Number(this, "RedChainCombat.SelectRequest.1", 6f)) { if (AttackRequest[1]) chosen = 1; }
        else if (distance >= BalanceRuntime.Number(this, "RedChainCombat.SelectRequest.2", 2.5f))
        {
            if (AttackRequest[0] && AttackRequest[2]) chosen = Random.value < BalanceRuntime.Number(this, "RedChainCombat.SelectRequest.3", 0.5f) ? 0 : 2;
            else if (AttackRequest[0]) chosen = 0;
            else if (AttackRequest[2]) chosen = 2;
        }
        else if (AttackRequest[0]) chosen = 0;
        else if (AttackRequest[2]) chosen = 2;
        ClearAttackRequests();
        if (chosen >= 0) AttackRequest[chosen] = true;
        return chosen >= 0;
    }

    public void BeginCast() { sampled = false; hitBoxes[ChainAttack].ResetChain(); }

    private float ChainTipX(float bodyX)//当前帧链头位置：动画图（紧切片）朝攻击方向的外缘
    {
        if (visual == null) visual = GetComponentInChildren<SpriteRenderer>();
        Rect limit = hitBoxes[ChainAttack].WorldRect;
        bool right = limit.center.x >= bodyX;
        if (visual == null || visual.sprite == null) return right ? limit.xMax : limit.xMin;
        return right ? visual.bounds.max.x : visual.bounds.min.x;
    }

    public void SampleFrame(int frame)
    {
        if (Attack < 0) return;
        //即使低帧率跳过了整个命中窗口，也只补采样一次，不丢砸地事件。
        if (!sampled && frame >= OpenFrame)
        {
            sampled = true;
            AttackBegin();
            Physics2D.SyncTransforms();
            if (Attack == 2) shockWave.Emit(this);
            if (Attack != ChainAttack) hitBoxes[Attack].SampleNow();
        }
        //链子出手后每个物理帧按当前链长采样，碰到即勾回；同一次出招只命中一次
        if (sampled && Attack == ChainAttack)
        {
            float bodyX = transform.position.x;
            hitBoxes[ChainAttack].SampleChain(bodyX, ChainTipX(bodyX));
        }
        if (AttackShut(frame)) AttackEnd();
        else if (sampled && AttackOpen(frame)) AttackBegin();
    }
}
