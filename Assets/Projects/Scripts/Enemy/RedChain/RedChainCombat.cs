using UnityEngine;

public sealed class RedChainCombat : EnemyCombat
{
    [SerializeField] private RedChainAttackBox[] hitBoxes;
    [SerializeField] private RedChainShockWaveEmitter shockWave;
    private bool sampled;
    private static readonly int[] openFrames = { 6, 8, 8 };
    private static readonly float[] windups = { 0.6f, 0.8f, 0.9f };
    protected override bool UsesAnger => false;
    public int OpenFrame => Attack >= 0 ? openFrames[Attack] : 0;
    public float Windup => Attack >= 0 ? windups[Attack] : 0f;

    public bool SelectRequest(float deltaX)
    {
        if (AttackRequest == null || AttackRequest.Length != 3) return false;
        int chosen = -1;
        float distance = Mathf.Abs(deltaX);
        if (distance > 6f) { if (AttackRequest[1]) chosen = 1; }
        else if (distance >= 2.5f)
        {
            if (AttackRequest[0] && AttackRequest[2]) chosen = Random.value < 0.5f ? 0 : 2;
            else if (AttackRequest[0]) chosen = 0;
            else if (AttackRequest[2]) chosen = 2;
        }
        else if (AttackRequest[0]) chosen = 0;
        else if (AttackRequest[2]) chosen = 2;
        ClearAttackRequests();
        if (chosen >= 0) AttackRequest[chosen] = true;
        return chosen >= 0;
    }

    public void BeginCast() => sampled = false;

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
            hitBoxes[Attack].SampleNow();
        }
        if (AttackShut(frame)) AttackEnd();
        else if (sampled && AttackOpen(frame)) AttackBegin();
    }
}
