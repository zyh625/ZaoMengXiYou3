using UnityEngine;

//绿色音符先升空再飞向天兵；玩家抢先接住时改为治疗玩家，不给敌人施加增幅。
public class BuffNoteManager : EastNoteManager
{
    [SerializeField, Min(0.01f)] private float ascendSpeed = 12f;//升空阶段与追向目标分别调节
    [SerializeField, Min(0.01f)] private float deliverySpeed = 9f;//目标移动时仍持续朝它飞行
    [SerializeField, Min(0.1f)] private float riseHeight = 4f;//先给玩家留下观察及抢接音符的时间
    [SerializeField, Min(0f)] private float enemyHealing = 30f;//天兵与Boss共用的固定回血量
    [SerializeField, Min(0f)] private float playerHealing = 20f;//玩家截获后恢复的固定血量
    [SerializeField, Min(0f)] private float attackMultiplier = 1.3f;//倍率1.3表示攻击提高30%，与怒气分别叠乘
    [SerializeField, Min(0f)] private float buffDuration = 5f;//同一Boss的重复增幅刷新持续时间，不指数叠加
    private Health recipient;//保存飞行目标，而不是飞行到达前就提前给目标回血
    private int recipientLifeVersion;//对象池的新生命不能接收追向旧生命的治疗
    private Vector2 apex;//升空端点在本次释放时固定
    private bool descending;//完成升空后才持续追向选中的天兵或Boss
    protected override bool IsHostile => false;

    protected override void OnLaunched(Vector2 position, bool right)
    {
        ChooseRecipient();
        float targetX = recipient != null ? RecipientCenter().x : position.x;
        apex = new Vector2(Mathf.Clamp(Mathf.Lerp(position.x, targetX, 0.5f), arenaBounds.x, arenaBounds.y),
            position.y + Mathf.Max(0.1f, riseHeight));
        descending = false;
    }

    private bool IsValidRecipient()//目标死亡、停用或池重生时重新选择，不把回血当成复活
        => recipient != null && recipient.isActiveAndEnabled && !recipient.Dead
            && recipient.LifeVersion == recipientLifeVersion;

    private void ChooseRecipient()//其他活天兵永远优先，血量更少者先接收，相同时选择更近者
    {
        recipient = null;
        float bestRatio = float.PositiveInfinity;
        float bestDistance = float.PositiveInfinity;
        foreach (EnemyMovement enemy in EnemyMovement.ActiveEnemies)
        {
            if (enemy == null || !enemy.isActiveAndEnabled || owner == null || enemy.gameObject == owner.gameObject) continue;
            if (enemy.GetComponent<PurpleHeavenlyCombat>() == null && enemy.GetComponent<PurpleRangedCombat>() == null) continue;
            Health health = enemy.GetComponent<Health>();
            if (health == null || health.Dead || !health.isActiveAndEnabled) continue;
            float ratio = health.BeginBlood > 0f ? health.CurBlood / health.BeginBlood : 1f;
            float distance = ((Vector2)enemy.transform.position - (Vector2)transform.position).sqrMagnitude;
            if (ratio > bestRatio || Mathf.Approximately(ratio, bestRatio) && distance >= bestDistance) continue;
            recipient = health;
            bestRatio = ratio;
            bestDistance = distance;
        }
        if (recipient == null && owner != null) recipient = owner.GetComponent<Health>();
        recipientLifeVersion = recipient != null ? recipient.LifeVersion : -1;
    }

    private Vector2 RecipientCenter()//以人物本体中心收束，人物切换动作帧时仍追向同一个身体
    {
        Collider2D body = recipient != null ? recipient.GetComponent<Collider2D>() : null;
        return body != null ? (Vector2)body.bounds.center : recipient != null ? (Vector2)recipient.transform.position : (Vector2)transform.position;
    }

    protected override void FixedUpdate()
    {
        if (!CanTick()) return;
        if (!IsValidRecipient()) ChooseRecipient();
        else if (owner != null && recipient.gameObject == owner.gameObject) ChooseRecipient();//升空后刚被召出的天兵也优先于Boss自己
        if (!IsValidRecipient())
        {
            StopEffect();
            return;
        }
        Vector2 from = transform.position;
        Vector2 destination = descending ? RecipientCenter() : apex;
        float speed = descending ? deliverySpeed : ascendSpeed;
        Vector2 to = Vector2.MoveTowards(from, destination, Mathf.Max(0.01f, speed) * Time.fixedDeltaTime);
        FaceDirection(to - from);
        bool hitPlayer = SweepPlayer(from, to, out Health player, out float playerFraction);
        Collider2D recipientBody = recipient.GetComponent<Collider2D>();
        float recipientFraction = 1f;//缺少碰撞箱的测试对象以实际中心到达为准
        bool hitRecipient = descending && recipientBody != null && recipientBody.enabled
            && SweepBounds(from, to, recipientBody.bounds, out recipientFraction);
        if (hitPlayer && (!hitRecipient || playerFraction <= recipientFraction))
        {
            OnPlayerContact(player);//只让真正先接触的玩家抢到，不隔着已经碰到的天兵截走治疗
            return;
        }
        if (hitRecipient)
        {
            GiveEnemyBuff();
            return;
        }
        SetPosition(to);
        if ((to - destination).sqrMagnitude > 0.000001f) return;
        if (!descending)
        {
            descending = true;
            return;
        }
        GiveEnemyBuff();
    }

    private void GiveEnemyBuff()//相交或到达中心后只交付一次，音符停用不会撤销已经施加的增益
    {
        if (IsValidRecipient())
        {
            recipient.Heal(Mathf.Max(0f, enemyHealing));
            EnemyCombat combat = recipient.GetComponent<EnemyCombat>();
            if (combat != null && owner != null)
                combat.ApplyAttackBuff(owner, Mathf.Max(0f, attackMultiplier), Mathf.Max(0f, buffDuration));
        }
        StopEffect();
    }

    protected override void OnPlayerContact(Health health)
    {
        if (health != null) health.Heal(Mathf.Max(0f, playerHealing));
        StopEffect();
    }

    protected override void OnDisable()
    {
        recipient = null;
        recipientLifeVersion = -1;
        descending = false;
        base.OnDisable();
    }
}
