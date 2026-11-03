using System.Collections.Generic;
using UnityEngine;

public class BoomerangManager : MonoBehaviour
{
    [SerializeField] private SpriteManager SM;//独立播放，不随沙僧换出而停止
    [SerializeField] private new SpriteData animation;//回旋镖的循环帧动画
    [SerializeField, Min(0.01f)] private float travelDistance = 6f;//从生成位置向前飞行的距离
    [SerializeField, Min(0.01f)] private float flySpeed = 8f;//去程速度
    [SerializeField, Min(0.01f)] private float returnSpeed = 8f;//回程与携带敌人的速度
    [SerializeField, Min(0f)] private float hitRadius = 0.25f;//扫掠命中检测的额外半径
    [SerializeField, Min(0f)] private float damage = 10f;//每名敌人每枚回旋镖只结算一次伤害
    private readonly HashSet<EnemyMovement> hitEnemies = new();//去程命中过的敌人即使离开回程路线也会被带回
    private readonly Dictionary<EnemyMovement, int> hitLifeVersions = new();//命中记录区分对象池同一引用的不同生命
    private readonly Dictionary<EnemyMovement, Vector2> carriedPositions = new();//本回旋镖独立拥有的携带目标与当前位置
    private readonly Dictionary<EnemyMovement, int> carriedLifeVersions = new();//携带与终点收束只允许作用于最初接管的那条生命
    private readonly List<EnemyMovement> enemies = new();//先快照再结算伤害，避免死亡改变活动敌人集合
    private readonly List<EnemyMovement> removed = new();//集中移除失效携带，避免遍历时修改字典
    private Transform caster;//伤害和经验保留真正的施法者
    private PlayerCombat combat;//命中时读取当前攻击倍率并增加无双能量
    private SpriteRenderer sr;
    private Vector2 origin;//回旋镖固定返回初次生成位置，空中释放也不改变高度
    private bool running, returning, faceRight;

    private void OnEnable()
    {
        ResolveVisual();
        running = returning = false;
        ReleaseEnemies();
        hitEnemies.Clear();
        hitLifeVersions.Clear();
    }

    public void Launch(Transform source, Vector2 position, bool right)//仅由PlayerManager创建、放置和回收
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        ReleaseEnemies();
        ResolveVisual();
        caster = source;
        combat = caster != null ? caster.GetComponent<PlayerCombat>() : null;
        origin = position;
        faceRight = right;
        hitEnemies.Clear();
        hitLifeVersions.Clear();
        returning = false;
        running = true;
        transform.position = new Vector3(position.x, position.y, transform.position.z);
        ShaEffectUtility.Play(SM, animation, sr, right);
    }

    private void FixedUpdate()
    {
        if (!running) return;
        if (!BattleRun.IsRunning) { StopEffect(); return; }//终局后释放自己携带的敌人并归还池
        Vector2 position = transform.position;
        Vector2 destination = returning ? origin : origin + Vector2.right * (faceRight ? travelDistance : -travelDistance);
        float speed = returning ? returnSpeed : flySpeed;
        Vector2 next = Vector2.MoveTowards(position, destination, Mathf.Max(0.01f, speed) * Time.fixedDeltaTime);
        transform.position = new Vector3(next.x, origin.y, transform.position.z);
        HitPath(position, next);
        if (!BattleRun.IsRunning) { StopEffect(); return; }//反伤等同步事件可能在本次命中时结束战斗
        if (returning) MoveEnemies();
        if ((next - destination).sqrMagnitude > 0.000001f) return;
        if (!returning) BeginReturn();
        else
        {
            foreach (EnemyMovement enemy in carriedPositions.Keys)
                if (IsCurrentCarry(enemy) && ShaEffectUtility.CanBeHit(enemy) && enemy.IsCarried)
                    enemy.UpdateCarryTarget(this, origin);//完整往返只收束原生命，不能把同池新敌人隔空带回
            StopEffect();
        }
    }

    private void HitPath(Vector2 from, Vector2 to)
    {
        enemies.Clear();
        foreach (EnemyMovement enemy in EnemyMovement.ActiveEnemies) enemies.Add(enemy);
        foreach (EnemyMovement enemy in enemies)
        {
            if (!BattleRun.IsRunning) break;//终局后不继续处理快照中的其他敌人，避免结算结束仍产生伤害
            if (!ShaEffectUtility.CanBeHit(enemy)
                || (hitEnemies.Contains(enemy) && hitLifeVersions.TryGetValue(enemy, out int version) && version == enemy.LifeVersion)
                || !ShaEffectUtility.SegmentHits(enemy, from, to, hitRadius)) continue;
            hitEnemies.Add(enemy);
            hitLifeVersions[enemy] = enemy.LifeVersion;//无敌目标不登记，回程恢复可受击后仍可正常命中
            Health health = enemy.GetComponent<Health>();
            if (!health.IsInvincible)
            {
                float previousBlood = health.CurBlood;
                health.TakeDamage(combat != null ? combat.CreateAttackDamage(damage, true, false, source: caster)
                    : new DamageInfo(caster, damage, true, false));//每名敌人每次真实命中只判定一次暴击
                if (combat != null && health.CurBlood < previousBlood) combat.UpdateBoost();
            }
            if (returning) CarryEnemy(enemy);
        }
    }

    private void BeginReturn()
    {
        returning = true;
        if (SM != null) SM.ChangeSpriteFace(!faceRight);//回程只翻转自身图像，不改变最初施法方向
        foreach (EnemyMovement enemy in hitEnemies) CarryEnemy(enemy);
    }

    private void CarryEnemy(EnemyMovement enemy)
    {
        if (!ShaEffectUtility.CanBeHit(enemy) || !hitLifeVersions.TryGetValue(enemy, out int version)
            || version != enemy.LifeVersion) return;//去程命中过的旧生命已消失时不能接管新生命
        if (carriedPositions.ContainsKey(enemy))
        {
            if (IsCurrentCarry(enemy)) return;
            carriedPositions.Remove(enemy);//仅丢弃旧生命的本地记录，不释放新生命的其他携带来源
            carriedLifeVersions.Remove(enemy);
        }
        if (enemy.IsCarried) return;
        Health health = enemy.GetComponent<Health>();
        if (!health.CanReceiveControl(ControlStrength.Strong)) return;//携带属于强控，普通霸体不免疫；无敌和死亡仍拒绝
        Vector2 position = enemy.transform.position;
        enemy.BeginCarry(this, position);//携带期间由敌人系统冻结受击帧、重力和自主移动
        if (enemy.IsCarried && enemy.LifeVersion == version)
        {
            carriedPositions.Add(enemy, position);
            carriedLifeVersions[enemy] = version;
        }
    }

    private bool IsCurrentCarry(EnemyMovement enemy)//不以Unity对象引用代替一次具体的敌人生命
        => enemy != null && carriedLifeVersions.TryGetValue(enemy, out int version) && version == enemy.LifeVersion;

    private void MoveEnemies()
    {
        removed.Clear();
        foreach (KeyValuePair<EnemyMovement, Vector2> item in carriedPositions)
        {
            EnemyMovement enemy = item.Key;
            if (!IsCurrentCarry(enemy))
            {
                removed.Add(enemy);//池对象复用后不得移动或释放新生命
                continue;
            }
            if (!ShaEffectUtility.CanBeHit(enemy) || !enemy.IsCarried)
            {
                if (enemy != null) enemy.EndCarry(this);
                removed.Add(enemy);
                continue;
            }
            Health health = enemy.GetComponent<Health>();
            if (!health.CanReceiveControl(ControlStrength.Strong))
            {
                enemy.EndCarry(this);
                removed.Add(enemy);
                continue;
            }
            Vector2 next = Vector2.MoveTowards(item.Value, origin, Mathf.Max(0.01f, returnSpeed) * Time.fixedDeltaTime);
            enemy.UpdateCarryTarget(this, next);
        }
        foreach (EnemyMovement enemy in removed)
        {
            carriedPositions.Remove(enemy);
            carriedLifeVersions.Remove(enemy);
        }
        //下一物理步从敌人的真实位置继续，不让重新获得霸体或被别的控制接管的对象留下旧坐标。
        enemies.Clear();
        foreach (EnemyMovement enemy in carriedPositions.Keys) enemies.Add(enemy);
        foreach (EnemyMovement enemy in enemies) carriedPositions[enemy] = enemy.transform.position;
    }

    private void ReleaseEnemies()
    {
        foreach (EnemyMovement enemy in carriedPositions.Keys)
            if (IsCurrentCarry(enemy)) enemy.EndCarry(this);//每个来源只释放自己接管的那条生命
        carriedPositions.Clear();
        carriedLifeVersions.Clear();
        removed.Clear();
        enemies.Clear();
    }

    private void ResolveVisual()
    {
        if (SM == null) SM = GetComponentInChildren<SpriteManager>(true);
        if (SM != null && sr == null) sr = SM.GetComponent<SpriteRenderer>();
    }

    public void StopEffect()//提前回收在当前位置释放目标，正常往返在返回点释放
    {
        running = false;
        ReleaseEnemies();
        hitEnemies.Clear();
        hitLifeVersions.Clear();
        ShaEffectUtility.Hide(SM, sr);
        if (gameObject.activeSelf) gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        running = false;
        ReleaseEnemies();
        hitEnemies.Clear();
        hitLifeVersions.Clear();
        ShaEffectUtility.Hide(SM, sr);
    }
}
