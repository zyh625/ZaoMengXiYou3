using System.Collections.Generic;
using UnityEngine;

public class FrostFormationManager : MonoBehaviour
{
    [SerializeField] private SpriteManager SM;//玄冰阵只显示与本体同步的帧，不自行计时。
    [SerializeField] private new SpriteData animation;//前八帧有效，后两帧碎裂。
    [SerializeField] private Rect[] frameBounds;//相对Sprite pivot的真实非透明范围。
    [SerializeField, Min(0f)] private float damage = 10f;//碎裂时对本次真正冰冻的敌人结算一次伤害。
    [SerializeField] private Vector2 localOffset;//领域相对人物的位置，通常以人物脚下为中心。
    private SpriteRenderer sr;
    private Transform caster;
    private PlayerCombat combat;
    private readonly Dictionary<EnemyMovement, int> frozenEnemies = new();//被冻结的具体生命不会因换帧或离开当前范围而丢失。
    private readonly List<EnemyMovement> enemies = new();//先登记当前活动敌人的快照，再施加冻结。
    private readonly List<FrozenTarget> breakTargets = new();//先解除所有冻结，再逐个造成碎裂伤害。
    private readonly struct FrozenTarget//碎裂队列也保存生命版本，避免连锁死亡事件命中新刷出的池成员。
    {
        public readonly EnemyMovement Enemy;
        public readonly int Life;
        public FrozenTarget(EnemyMovement enemy, int life) { Enemy = enemy; Life = life; }
    }
    private int lastBodyFrame = -1;
    private bool casting, shattered;//后两张碎裂图共用一次伤害，不能各打一次。

    private void OnEnable()
    {
        TangSkillEffectUtility.Resolve(gameObject, ref SM, ref sr);
        StopEffect();
    }

    public void BeginCast(Transform source)//开始时清理旧施法，不能继承上一轮的冻结名单。
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        StopEffect();
        TangSkillEffectUtility.Resolve(gameObject, ref SM, ref sr);
        caster = source;
        combat = caster != null ? caster.GetComponent<PlayerCombat>() : null;
        casting = true;
        shattered = false;
    }

    public void SyncFrame(int bodyFrame, bool right)
    {
        if (!casting) return;
        TangSkillEffectUtility.Place(transform, caster, localOffset, right);
        if (!shattered)
        {
            int first = Mathf.Max(lastBodyFrame + 1, 4);
            int last = Mathf.Min(bodyFrame, 11);
            for (int frame = first; frame <= last && casting; frame++) FreezeFrame(frame - 4);
            if (bodyFrame >= 4 && bodyFrame <= 11) FreezeFrame(bodyFrame - 4);
            if (bodyFrame >= 12) Shatter();//即使渲染跳过第13帧，也不能漏掉已经完成的碎裂阶段。
        }
        if (!casting) return;
        if (bodyFrame >= 4 && bodyFrame <= 13)
            TangSkillEffectUtility.Show(SM, sr, animation, bodyFrame - 4);
        else TangSkillEffectUtility.Hide(SM, sr);
        lastBodyFrame = bodyFrame;
        if (bodyFrame >= 14) StopEffect();//本体第15帧起玄冰阵不再显示或命中。
    }

    private void FreezeFrame(int frame)//前四帧逐步扩大，后四帧沿用第四帧的范围。
    {
        if (!TangSkillEffectUtility.TryBounds(transform, sr, frameBounds, Mathf.Min(frame, 3), out Bounds area)) return;
        enemies.Clear();
        foreach (EnemyMovement enemy in EnemyMovement.ActiveEnemies) enemies.Add(enemy);
        for (int i = 0; i < enemies.Count && casting; i++)
        {
            EnemyMovement enemy = enemies[i];
            if (enemy == null || !enemy.CanReceiveHit || !TangSkillEffectUtility.Touches(enemy, area)) continue;
            int life = enemy.LifeVersion;
            if (frozenEnemies.TryGetValue(enemy, out int previous) && previous == life) continue;//一条生命本轮只施加一次，不逐帧重置冰块的融化计时
            ActorStatus status = enemy.GetComponent<Health>().Status;//玩家与敌人使用同一套冰冻表现、融化和破冰增伤
            if (status != null && status.ApplyFreeze(this) && enemy.LifeVersion == life && status.HasFreeze(this))
                frozenEnemies[enemy] = life;//真正被冰冻的目标才有资格进入碎裂名单
        }
    }

    private void Shatter()//本轮只碎裂一次，清理冻结后才造成伤害。
    {
        if (shattered) return;
        shattered = true;
        breakTargets.Clear();
        foreach (KeyValuePair<EnemyMovement, int> pair in frozenEnemies)
        {
            EnemyMovement enemy = pair.Key;
            if (enemy == null || enemy.LifeVersion != pair.Value) continue;
            ActorStatus status = enemy.GetComponent<Health>().Status;
            if (status != null && status.HasFreeze(this)) breakTargets.Add(new FrozenTarget(enemy, pair.Value));//提前受击破冰或被驱散后，不凭旧名单再次结伤
        }
        frozenEnemies.Clear();//伤害事件可能打断施法，此时清理不会再重复遍历旧冻结名单。
        for (int i = 0; i < breakTargets.Count && casting; i++)
        {
            FrozenTarget target = breakTargets[i];
            EnemyMovement enemy = target.Enemy;
            if (enemy == null || enemy.LifeVersion != target.Life) continue;
            Health health = enemy.GetComponent<Health>();
            if (health.Status == null || !health.Status.HasFreeze(this)) continue;//前一个目标的连锁效果可能已经破除了后续目标的冰块
            if (!enemy.CanReceiveHit)
            {
                health.Status?.CancelFreeze(this);//突然无敌的目标不结伤，但不能遗留本轮冰冻
                continue;
            }
            float blood = health.CurBlood;
            //Health先破除冰块并计入通用倍率，再结算本次碎裂伤害，不能先CancelFreeze丢失1.5倍。
            health.TakeDamage(combat != null ? combat.CreateAttackDamage(damage, false, false, source: caster)
                : new DamageInfo(caster, damage, false, false));//破冰倍率仍由Health结算，不能提前重复计算
            if (combat != null && health.CurBlood < blood) combat.UpdateBoost();
        }
        breakTargets.Clear();
    }

    public void StopEffect()//提前中断只解除冰冻，不把中断当成正常碎裂造成伤害。
    {
        casting = false;
        foreach (KeyValuePair<EnemyMovement, int> pair in frozenEnemies)
            if (pair.Key != null && pair.Key.LifeVersion == pair.Value) pair.Key.GetComponent<Health>().Status?.CancelFreeze(this);
        frozenEnemies.Clear();
        foreach (FrozenTarget target in breakTargets)//碎裂结伤中的反伤也可能打断施法，尚未处理的冰块必须一起释放
            if (target.Enemy != null && target.Enemy.LifeVersion == target.Life)
                target.Enemy.GetComponent<Health>().Status?.CancelFreeze(this);
        enemies.Clear();
        breakTargets.Clear();
        lastBodyFrame = -1;
        TangSkillEffectUtility.Hide(SM, sr);
    }

    private void OnDisable() => StopEffect();
}
