using UnityEngine;

public class EastCombat : BossCombat
{
    [Header("音符攻击（白、红、蓝、绿）")]
    [SerializeField] private EastNoteManager[] notePrefabs = new EastNoteManager[4];//独立音符模板，由场景对象池创建发射实例
    [SerializeField] private float[] noteCooldowns = { 2f, 6f, 10f, 8f };//各招式从成功起手时分别计算冷却，顺序与音符模板一致
    [SerializeField, Min(0f)] private float minimumAttackInterval = 0.75f;//一轮出招完成或被打断后，至少休息此时长再开始下一轮
    [SerializeField, Min(0f)] private float firstAttackDelay = 1f;//刚现身时给玩家准备时间，之后仅由各自冷却和公共间隔决定
    [SerializeField, Min(0)] private int shootFrame = 9;//弹琴动作第十帧发射，下标从零开始

    private readonly float[] lastNoteTimes = new float[4];//只记录真正开始的出招，查询准备状态不会消耗冷却
    private Health health;//统一检查死亡、受击与冰冻，禁止被打断后补发音符
    private EastMovement movement;//普通眩晕与携带期间同样禁止新起手或继续发射
    private bool castActive, noteReleased;//本轮动作和已发射标记分开，重复检查动作帧只发射一次
    private int currentNote = -1, nextNote;//依次轮询已就绪招式，防止普通攻击一直抢占其他音符
    private float nextAttackTime, lastAttackStartedAt;//公共收招间隔与本次起手时间独立计算

    public bool IsCasting => castActive;
    public bool NoteReleased => noteReleased;
    public int CurrentNoteIndex => currentNote;
    public float LastAttackStartedAt => lastAttackStartedAt;

    protected override void Awake()
    {
        base.Awake();
        ResolveReferences();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        ResolveReferences();
        castActive = noteReleased = false;
        currentNote = -1;
        nextNote = 0;
        nextAttackTime = Time.time + Mathf.Max(0f, firstAttackDelay);
        lastAttackStartedAt = float.NegativeInfinity;
        for (int i = 0; i < lastNoteTimes.Length; i++) lastNoteTimes[i] = float.NegativeInfinity;
        if (health != null) health.Died += OnDied;
    }

    private void ResolveReferences()//兼容对象池启用顺序，不把当前目标或场景对象写回预制体
    {
        if (health == null) health = GetComponent<Health>();
        if (movement == null) movement = GetComponent<EastMovement>();
    }

    private bool CanAct()//只管演员自身是否可出招，不设置距离或攻击检测箱要求
        => isActiveAndEnabled && health != null && !health.Dead
            && (!health.Attacked || health.Boost)
            && !(health.Status != null && health.Status.BlocksActions)
            && (movement == null || !movement.IsStunned && !movement.IsCarried);

    public int GetReadyAttack()//返回下一种已冷却的有效音符，所有招式都没有距离限制
    {
        if (castActive || !CanAct() || Time.time < nextAttackTime) return -1;
        for (int step = 0; step < lastNoteTimes.Length; step++)
        {
            int index = (nextNote + step) % lastNoteTimes.Length;
            if (notePrefabs == null || index >= notePrefabs.Length || notePrefabs[index] == null) continue;
            if (Time.time - lastNoteTimes[index] >= CooldownFor(index)) return index;
        }
        return -1;
    }

    private float CooldownFor(int index)//错误或缺失的冷却配置不会造成越界或永久卡住
        => noteCooldowns != null && index < noteCooldowns.Length && !float.IsNaN(noteCooldowns[index])
            ? Mathf.Max(0f, noteCooldowns[index]) : 0f;

    public bool BeginAttack(int index, bool right)//控制器真正切到弹琴动作时才消耗本招冷却
    {
        if (index < 0 || index >= lastNoteTimes.Length || castActive || !CanAct()
            || Time.time < nextAttackTime || notePrefabs == null || index >= notePrefabs.Length
            || notePrefabs[index] == null || Time.time - lastNoteTimes[index] < CooldownFor(index)) return false;
        currentNote = index;
        nextNote = (index + 1) % lastNoteTimes.Length;
        castActive = true;
        noteReleased = false;
        AttackFaceRight = right;
        lastNoteTimes[index] = lastAttackStartedAt = Time.time;
        health.Status?.NotifyAttackStarted();
        return true;
    }

    public void CheckNote(int frame)//物理更新和渲染更新可共用此入口，跨过第十帧也不会漏发或重复发射
    {
        if (!castActive || noteReleased) return;
        if (!CanAct())
        {
            CancelAttack();
            return;
        }
        if (frame < Mathf.Max(0, shootFrame)) return;
        noteReleased = true;
        EastNoteManager prefab = notePrefabs != null && currentNote >= 0 && currentNote < notePrefabs.Length
            ? notePrefabs[currentNote] : null;
        if (prefab != null)
            EastNotePool.GetOrCreate().Fire(prefab, this, transform.position, AttackFaceRight);
    }

    public void CancelAttack()//不回收已经飞出的独立音符，只丢弃本轮尚未执行的发射请求
    {
        if (!castActive) return;
        castActive = false;
        currentNote = -1;
        nextAttackTime = Time.time + Mathf.Max(0f, minimumAttackInterval);
    }

    private void OnDied(DamageInfo damage) => CancelAttack();//死亡不允许在下一次LateUpdate补出投射物

    private void OnDisable()//对象池禁用时退订，下一次出现重新计算首轮准备时间与冷却
    {
        if (health != null) health.Died -= OnDied;
        CancelAttack();
        noteReleased = false;
    }
}
