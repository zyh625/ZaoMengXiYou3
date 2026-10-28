using System.Collections.Generic;
using UnityEngine;

public class OceanBreakerManager : MonoBehaviour
{
    [SerializeField] private SpriteManager SM;//只暂停自己的播放器，不影响唐僧本体。
    [SerializeField] private SpriteData animation;//八帧水柱，由本体第5至12帧逐帧驱动。
    [SerializeField] private Rect[] frameBounds;//实际图片的非透明范围，相对每帧pivot。
    [SerializeField, Min(0f)] private float damage = 20f;//每次完整施法对每条敌人生命只造成一次伤害。
    [SerializeField] private Vector2 localOffset;//水柱相对角色的位置，朝左时水平镜像。
    private SpriteRenderer sr;//显示和判定共用同一个特效变换。
    private Transform caster;//保存本次施法来源，用于伤害方向、倍率和无双积累。
    private PlayerCombat combat;
    private readonly Dictionary<EnemyMovement, int> hitEnemies = new();//记录对象池生命版本，新刷出的敌人可以重新命中。
    private readonly List<EnemyMovement> enemies = new();//伤害前快照，避免死亡回收改变活动集合。
    private int lastBodyFrame = -1;//跨帧时补齐遗漏的有效范围，避免低帧率漏掉整段水柱。
    private bool casting;//被打断后即使本体仍显示旧帧，也不能补放已经结束的效果。

    private void OnEnable()
    {
        TangSkillEffectUtility.Resolve(gameObject, ref SM, ref sr);
        StopEffect();
    }

    public void BeginCast(Transform source)//只在真正进入技能动作后开始新的命中记录。
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        StopEffect();
        TangSkillEffectUtility.Resolve(gameObject, ref SM, ref sr);
        caster = source;
        combat = caster != null ? caster.GetComponent<PlayerCombat>() : null;
        casting = true;
    }

    public void SyncFrame(int bodyFrame, bool right)//本体与水柱共用帧号，不采用第二套fps计时。
    {
        if (!casting) return;
        TangSkillEffectUtility.Place(transform, caster, localOffset, right);
        int first = Mathf.Max(lastBodyFrame + 1, 4);//本体下标4即用户看到的第5帧。
        int last = Mathf.Min(bodyFrame, 11);
        for (int frame = first; frame <= last && casting; frame++) DamageFrame(frame - 4);
        if (!casting) return;
        if (bodyFrame >= 4 && bodyFrame <= 11)
        {
            DamageFrame(bodyFrame - 4);//同一动作帧内进入水柱的敌人同样会受到一次攻击。
            if (casting) TangSkillEffectUtility.Show(SM, sr, animation, bodyFrame - 4);
        }
        else TangSkillEffectUtility.Hide(SM, sr);
        lastBodyFrame = bodyFrame;
        if (bodyFrame >= 12) StopEffect();//本体第13帧到来时立即关闭，不等整套本体动画结束。
    }

    private void DamageFrame(int frame)//扫过当前水柱实际覆盖的路径，命中过的同一生命不重复结算。
    {
        if (!TangSkillEffectUtility.TryBounds(transform, sr, frameBounds, Mathf.Min(frame, 3), out Bounds area)) return;
        enemies.Clear();
        foreach (EnemyMovement enemy in EnemyMovement.ActiveEnemies) enemies.Add(enemy);
        for (int i = 0; i < enemies.Count && casting; i++)
        {
            EnemyMovement enemy = enemies[i];
            if (enemy == null || !enemy.CanReceiveHit || !TangSkillEffectUtility.Touches(enemy, area)) continue;
            int life = enemy.LifeVersion;//登记在伤害之前，避免同一次死亡事件里再度命中。
            if (hitEnemies.TryGetValue(enemy, out int previous) && previous == life) continue;
            hitEnemies[enemy] = life;
            Health health = enemy.GetComponent<Health>();
            float blood = health.CurBlood;
            health.TakeDamage(combat != null ? combat.CreateAttackDamage(damage, source: caster)
                : new DamageInfo(caster, damage));//沿用同一套攻击加成与单次暴击判定
            if (combat != null && health.CurBlood < blood) combat.UpdateBoost();
        }
    }

    public void StopEffect()//受击、死亡、换人和技能结束都通过相同入口关闭水柱。
    {
        casting = false;
        lastBodyFrame = -1;
        hitEnemies.Clear();
        enemies.Clear();
        TangSkillEffectUtility.Hide(SM, sr);
    }

    private void OnDisable() => StopEffect();
}
