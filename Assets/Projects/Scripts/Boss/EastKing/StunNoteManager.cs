using UnityEngine;

//红色音符持续追踪当前上场角色，命中时通过Health的效果接口施加普通眩晕。
public class StunNoteManager : EastNoteManager
{
    [SerializeField, Min(0.01f)] private float trackingSpeed = 4f;//独立于白色高速音符的缓慢追踪速度
    [SerializeField, Min(0f)] private float stunDuration = 2f;//普通霸体可以抵抗，开无双可以驱散
    private EastNoteStunEffect stunEffect;//每个池成员只创建一份效果资源，复用时只刷新配置
    private AttackEffect[] hitEffects;//完整命中结算通过效果数组执行，格挡整次攻击时不会单独被晕
    protected override AttackEffect[] HitEffects => hitEffects;

    protected override void OnLaunched(Vector2 position, bool right)
    {
        if (stunEffect == null)
        {
            stunEffect = ScriptableObject.CreateInstance<EastNoteStunEffect>();
            stunEffect.hideFlags = HideFlags.HideAndDontSave;
            hitEffects = new AttackEffect[] { stunEffect };
        }
        stunEffect.Duration = Mathf.Max(0f, stunDuration);
    }

    protected override void FixedUpdate()
    {
        if (!CanTick()) return;
        Vector2 from = transform.position;
        if (TryGetPlayer(out _, out Collider2D body))
        {
            Vector2 aim = (Vector2)body.bounds.center - from;
            if (aim.sqrMagnitude > 0.000001f) direction = aim.normalized;
        }
        FaceDirection(direction);
        Vector2 to = from + direction * (Mathf.Max(0.01f, trackingSpeed) * Time.fixedDeltaTime);
        if (SweepPlayer(from, to, out Health player, out _))
        {
            OnPlayerContact(player);
            return;
        }
        SetPosition(to);
    }

    private void OnDestroy()//效果资源只在池对象真正销毁时释放，命中后的人物眩晕不依赖音符继续存活
    {
        if (stunEffect != null) Destroy(stunEffect);
    }
}
