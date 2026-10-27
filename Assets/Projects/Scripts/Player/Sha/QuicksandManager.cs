using System.Collections.Generic;
using UnityEngine;

public class QuicksandManager : MonoBehaviour
{
    [SerializeField] private SpriteManager SM;//领域与沙僧分开播放，换人后仍保留
    [SerializeField] private SpriteData animation;//流沙的八帧循环动画
    [SerializeField, Min(0f)] private float duration = 10f;//领域存在时间
    [SerializeField, Min(0.01f)] private float radius = 3f;//领域水平方向的作用半径
    [SerializeField, Range(0f, 1f)] private float edgeSlowAmount = 0.6f;//边缘减速幅度，中心总是减速100%
    [SerializeField, Min(0f)] private float pullSpeed = 1.5f;//额外朝领域中心拉动的固定水平速度
    [SerializeField, Min(0f)] private float groundTolerance = 0.25f;//敌人脚底与领域地面的允许高度差
    [SerializeField, Min(0.01f)] private float poisonInterval = 1f;//连续站在领域内多久叠加一层毒
    [SerializeField, Min(0f)] private float poisonDuration = 5f;//与麻痹药剂共用毒层与刷新规则
    [SerializeField, Min(0f)] private float poisonDamagePerStack = 1f;//每层毒每秒造成的伤害
    private readonly HashSet<EnemyMovement> affected = new();//本领域已施加减速的目标
    private readonly Dictionary<EnemyMovement, int> affectedLifeVersions = new();//池中重生的新敌人不能继承旧生命的连续停留计时
    private readonly HashSet<EnemyMovement> present = new();//本物理步仍然踩在领域上的目标
    private readonly Dictionary<EnemyMovement, float> poisonElapsed = new();//离开领域会重置连续停留时间
    private readonly List<EnemyMovement> removed = new();//移除自身效果时不修改遍历集合
    private Transform caster;//持续伤害保留施法者，不因换人失去经验归属
    private SpriteRenderer sr;
    private Vector2 origin;//生成时确定地面位置，领域不跟随角色
    private float launchedAt, activePoisonDuration, activePoisonDamage;
    private bool running;

    private void OnEnable()
    {
        ResolveVisual();
        running = false;
        ReleaseEnemies();
    }

    public void Launch(Transform source, Vector2 groundPosition, float poisonTime = -1f, float damagePerStack = -1f)//地面位置由PlayerManager向下探测后传入
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        ReleaseEnemies();
        ResolveVisual();
        caster = source;
        origin = groundPosition;
        transform.position = new Vector3(origin.x, origin.y, transform.position.z);
        activePoisonDuration = poisonTime >= 0f ? poisonTime : poisonDuration;
        activePoisonDamage = damagePerStack >= 0f ? damagePerStack : poisonDamagePerStack;
        launchedAt = Time.time;
        running = true;
        ShaEffectUtility.Play(SM, animation, sr, true);
        if (duration <= 0f) StopEffect();
    }

    private void FixedUpdate()
    {
        if (!running) return;
        if (Time.time - launchedAt >= duration)
        {
            StopEffect();
            return;
        }
        present.Clear();
        float deltaTime = Time.fixedDeltaTime;
        float interval = Mathf.Max(0.01f, poisonInterval);
        foreach (EnemyMovement enemy in EnemyMovement.ActiveEnemies)
        {
            if (!ShaEffectUtility.CanBeHit(enemy) || !enemy.IsOnGroundNow()) continue;//关闭受击箱和无敌期间不新增减速、牵引与毒层
            Bounds bounds = ShaEffectUtility.GetBounds(enemy);
            float deltaX = origin.x - bounds.center.x;
            if (Mathf.Abs(deltaX) > radius || Mathf.Abs(bounds.min.y - origin.y) > groundTolerance) continue;
            present.Add(enemy);
            if (!affectedLifeVersions.TryGetValue(enemy, out int version) || version != enemy.LifeVersion)
                poisonElapsed.Remove(enemy);//同一个池对象的新生命从零累计这一秒
            affectedLifeVersions[enemy] = enemy.LifeVersion;
            affected.Add(enemy);
            float distanceRatio = Mathf.Clamp01(Mathf.Abs(deltaX) / Mathf.Max(0.01f, radius));
            float moveRatio = (1f - edgeSlowAmount) * distanceRatio;//边缘保留40%移速，靠近中心逐渐降为零
            float pullVelocity = Mathf.Sign(deltaX) * Mathf.Min(pullSpeed, Mathf.Abs(deltaX) / deltaTime);
            enemy.SetQuicksand(this, moveRatio, pullVelocity, origin.x);//敌人系统同时钳制自主移动与牵引，避免跨过中心后抖动
            poisonElapsed.TryGetValue(enemy, out float elapsed);
            elapsed += deltaTime;
            while (elapsed >= interval)
            {
                enemy.GetComponent<Health>().ApplyPoison(caster, activePoisonDuration, activePoisonDamage);
                elapsed -= interval;
            }
            poisonElapsed[enemy] = elapsed;
        }
        removed.Clear();
        foreach (EnemyMovement enemy in affected)
            if (!present.Contains(enemy)) removed.Add(enemy);
        foreach (EnemyMovement enemy in removed)
        {
            if (IsCurrentLife(enemy)) enemy.RemoveQuicksand(this);
            affected.Remove(enemy);
            affectedLifeVersions.Remove(enemy);
            poisonElapsed.Remove(enemy);
        }
    }

    private bool IsCurrentLife(EnemyMovement enemy)//清理自己的旧记录时不得修改池对象的新生命
        => enemy != null && affectedLifeVersions.TryGetValue(enemy, out int version) && version == enemy.LifeVersion;

    private void ReleaseEnemies()
    {
        foreach (EnemyMovement enemy in affected)
            if (IsCurrentLife(enemy)) enemy.RemoveQuicksand(this);//只移除本领域在当前生命上的效果
        affected.Clear();
        affectedLifeVersions.Clear();
        present.Clear();
        poisonElapsed.Clear();
        removed.Clear();
    }

    private void ResolveVisual()
    {
        if (SM == null) SM = GetComponentInChildren<SpriteManager>(true);
        if (SM != null && sr == null) sr = SM.GetComponent<SpriteRenderer>();
    }

    public void StopEffect()//领域到期不会立即清除已经叠加的毒，毒按自己的剩余时间结束
    {
        running = false;
        ReleaseEnemies();
        ShaEffectUtility.Hide(SM, sr);
        if (gameObject.activeSelf) gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        running = false;
        ReleaseEnemies();
        ShaEffectUtility.Hide(SM, sr);
    }
}
