using UnityEngine;

//运行期命中效果由Health接受整次攻击后执行，避免无敌或一次性格挡仍附带眩晕。
public class EastNoteStunEffect : AttackEffect
{
    public float Duration { get; set; }//由所属红音符在发射时读取Inspector中的持续时间

    public override void Apply(Health target, DamageInfo damage)
    {
        if (target == null || Duration <= 0f || !target.CanReceiveControl()) return;
        ActorStun stun = target.GetComponent<ActorStun>();
        if (stun == null) stun = target.gameObject.AddComponent<ActorStun>();
        stun.Apply(damage.Source, Duration);
    }
}
