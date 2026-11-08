using System.Collections;
using UnityEngine;

//Boss 表现层的共用部分：登场光效、死亡光效、身后光环、上升光点。只读战斗状态，不影响判定。
public abstract class BossFxBase : MonoBehaviour
{
    protected SpriteRenderer body;
    protected Health health;
    private FxInstance aura;
    private float nextMote;

    protected virtual void Awake()
    {
        health = GetComponent<Health>();
        body = GetComponentInChildren<SpriteRenderer>(true);
    }

    protected virtual void OnEnable()
    {
        if (health != null) health.Died += OnDied;
        if (BattleRun.IsRunning) StartCoroutine(Entrance());//正式出场；对象池初始化时同一帧就会停用，不会播放
    }

    protected virtual void OnDisable()
    {
        if (health != null) health.Died -= OnDied;
        StopAura();
    }

    protected Bounds B => body != null ? body.bounds : new Bounds(transform.position, Vector3.one * 2f);
    protected Vector3 Feet => new(B.center.x, B.min.y, 0f);
    protected bool Alive => health == null || !health.Dead;

    private IEnumerator Entrance()
    {
        yield return null;
        if (body == null) yield break;
        PlayEntrance();
    }

    protected abstract void PlayEntrance();
    protected abstract void PlayDeath();
    private void OnDied(DamageInfo damage) { StopAura(); PlayDeath(); }

    protected FxInstance Burst(string clip, Vector3 at, float size, Color color, float life, float growFrom, float growTo, int order, float squash = 1f, float rotation = 0f)
    {
        FxSpec s = Fx.Make(clip, at, size, color);
        s.life = life; s.growFrom = growFrom; s.growTo = growTo; s.fadeOut = Mathf.Min(0.3f, life * 0.6f); s.squashY = squash; s.rotation = rotation;
        Fx.SortLike(ref s, body, order);
        return Fx.Spawn(s);
    }

    protected void Aura(bool on, Color color, Color moteColor)//强化状态：身后闪烁光环 + 身上光点上升
    {
        if (!on || !Alive) { StopAura(); return; }
        if (aura == null || !aura.Playing)
        {
            FxSpec s = Fx.Make("sun", B.center - transform.position, B.size.y * 1.7f, color);
            s.follow = transform; s.loop = true; s.fadeIn = 0.3f;
            Fx.SortLike(ref s, body, -3);
            aura = Fx.Spawn(s);
        }
        if (Time.time < nextMote) return;
        nextMote = Time.time + 0.1f;
        Bounds b = B;
        FxSpec m = Fx.Make(null, new Vector3(b.center.x + Random.Range(-b.extents.x, b.extents.x) * 0.8f, b.min.y + Random.Range(0f, b.size.y * 0.5f), 0f),
            Random.Range(0.25f, 0.45f), moteColor);
        m.sprite = FxTextures.Spark; m.life = 1f; m.fadeIn = 0.15f; m.fadeOut = 0.5f; m.drift = new Vector2(Random.Range(-0.2f, 0.2f), 1.6f);
        Fx.SortLike(ref m, body, 2);
        Fx.Spawn(m);
    }

    private void StopAura()
    {
        if (aura != null) aura.Stop(0.4f);
        aura = null;
    }
}

//持国天王（琵琶）：每次弹出音符，琵琶处荡开与音符同色的声波；绿色召唤音符在前方地面亮起法阵；
//“急弦”强化期间身后紫金色光环闪烁、光点上升，并且每隔一会儿荡开一圈声波。
public sealed class EastKingFx : BossFxBase
{
    private EastCombat combat;
    private bool lastReleased;
    private float nextPulse;
    private static readonly Color[] NoteColors =
    {
        new(1f, 1f, 1f), new(1f, 0.35f, 0.3f), new(0.45f, 1f, 0.5f), new(0.45f, 0.72f, 1f),//白、红（眩晕）、绿（召唤）、蓝（增益）
    };
    private static readonly Color Violet = new(0.85f, 0.55f, 1f, 0.5f);

    protected override void Awake() { base.Awake(); combat = GetComponent<EastCombat>(); }

    private void Update()
    {
        if (combat == null || body == null) return;
        bool released = combat.NoteReleased;
        if (released && !lastReleased && combat.CurrentNoteIndex >= 0) OnNote(combat.CurrentNoteIndex);
        lastReleased = released;

        bool rapid = combat.RapidStringsActive;
        Aura(rapid, Violet, new Color(1f, 0.8f, 1f, 0.9f));
        if (rapid && Time.time >= nextPulse && Alive)
        {
            nextPulse = Time.time + 0.6f;
            Burst("ring", B.center, B.size.y * 0.5f, new Color(0.9f, 0.65f, 1f, 0.6f), 0.6f, 1f, 3.2f, -2);
        }
    }

    private float Facing => combat.AttackFaceRight ? 1f : -1f;

    private void OnNote(int index)
    {
        Color c = NoteColors[Mathf.Clamp(index, 0, NoteColors.Length - 1)];
        Bounds b = B;
        Vector3 pipa = b.center + new Vector3(Facing * b.extents.x * 0.35f, b.size.y * 0.05f, 0f);
        Burst("ring", pipa, b.size.y * 0.3f, new Color(c.r, c.g, c.b, 0.95f), 0.45f, 1f, 4.5f, 3);
        Burst("ring", pipa, b.size.y * 0.25f, new Color(c.r, c.g, c.b, 0.6f), 0.65f, 1f, 6f, 2);
        Burst("sun", pipa, b.size.y * 0.55f, new Color(c.r, c.g, c.b, 0.85f), 0.3f, 0.8f, 1.2f, 4);
        if (index == 1) Burst("gold_spark", pipa, b.size.y * 0.6f, new Color(1f, 0.45f, 0.35f), 0.3f, 0.8f, 1.4f, 5);
        if (index == 2)//召唤：前方地面亮起绿色法阵
        {
            FxSpec s = Fx.Make("rune_green", new Vector3(b.center.x + Facing * b.size.x * 0.9f, b.min.y + 0.05f, 0f), b.size.x * 1.4f, Color.white);
            s.squashY = 0.35f; s.life = 1.4f; s.fadeIn = 0.1f; s.fadeOut = 0.5f;
            Fx.SortLike(ref s, body, -2);
            Fx.Spawn(s);
        }
    }

    protected override void PlayEntrance()
    {
        Bounds b = B;
        Burst("gold_burst", b.center, b.size.y * 1.6f, Color.white, 0.5f, 0.6f, 1.4f, 4);
        Burst("ring", Feet + Vector3.up * 0.05f, b.size.x, new Color(1f, 0.85f, 0.4f), 0.7f, 0.6f, 4f, -2, 0.35f);
        Burst("ring", b.center, b.size.y * 0.4f, new Color(0.9f, 0.65f, 1f, 0.8f), 0.6f, 1f, 5f, 3);
    }

    protected override void PlayDeath()
    {
        Bounds b = B;
        Burst("explosion", b.center, b.size.y * 1.3f, Color.white, 0.45f, 1f, 1.3f, 5);
        Burst("gold_burst", b.center, b.size.y * 1.8f, Color.white, 0.6f, 0.8f, 1.6f, 6);
        Burst("ring", b.center, b.size.y * 0.5f, new Color(1f, 0.85f, 0.4f), 0.8f, 1f, 6f, 4);
    }
}

//增长天王（冰）：冰锥破土时地面炸开冰晶和寒气环；剑阵从天空的冰色法阵中落下；起护盾时寒气外放；
//识破时金光一闪；“寒霜”强化期间身后冰蓝光环、冰晶光点上升。
public sealed class SouthKingFx : BossFxBase
{
    private SouthKingCombat combat;
    private static readonly Color Ice = new(0.6f, 0.9f, 1f);

    protected override void Awake() { base.Awake(); combat = GetComponent<SouthKingCombat>(); }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (combat == null) return;
        combat.SkillReleased += OnSkill;
        combat.CounterRecognized += OnCounter;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (combat == null) return;
        combat.SkillReleased -= OnSkill;
        combat.CounterRecognized -= OnCounter;
    }

    private void Update()
    {
        if (combat != null && body != null) Aura(combat.FrostActive, new Color(0.45f, 0.8f, 1f, 0.5f), new Color(0.75f, 0.95f, 1f, 0.95f));
    }

    private void OnSkill(SouthKingCombat.KingSkill skill, Vector2 at)
    {
        Bounds b = B;
        switch (skill)
        {
            case SouthKingCombat.KingSkill.IceSpikes:
                Burst("ice_shatter", (Vector3)at + Vector3.up * 0.4f, 2.4f, Color.white, 0.5f, 0.5f, 1.4f, 4);
                Burst("ring", (Vector3)at + Vector3.up * 0.05f, 1.2f, Ice, 0.6f, 0.6f, 4.5f, -2, 0.35f);
                break;
            case SouthKingCombat.KingSkill.SwordFormation://天空出现横向的冰色法阵，剑从里面落下
            {
                FxSpec s = Fx.Make("portal", (Vector3)at, 3.6f, new Color(0.7f, 0.95f, 1f, 0.95f));
                s.loop = true; s.life = 1.4f; s.rotation = 90f; s.fadeIn = 0.15f; s.fadeOut = 0.5f; s.growFrom = 0.4f; s.growTo = 1.1f;
                Fx.SortLike(ref s, body, 6);
                Fx.Spawn(s);
                Burst("sun", (Vector3)at, 1.8f, Ice, 0.5f, 0.6f, 1.2f, 5);
                break;
            }
            case SouthKingCombat.KingSkill.IceShield:
                Burst("ring", b.center, b.size.y * 0.4f, Ice, 0.6f, 1f, 5f, 4);
                Burst("ice_shatter", b.center, b.size.y * 1.1f, new Color(1f, 1f, 1f, 0.9f), 0.45f, 0.7f, 1.5f, 5);
                break;
        }
    }

    private void OnCounter()//识破：金光一闪 + 金色星芒
    {
        Bounds b = B;
        Burst("gold_burst", b.center, b.size.y * 1.5f, Color.white, 0.35f, 0.7f, 1.3f, 6);
        Burst("gold_spark", b.center + Vector3.up * b.size.y * 0.15f, b.size.y * 0.9f, Color.white, 0.3f, 0.6f, 1.5f, 7);
    }

    protected override void PlayEntrance()
    {
        Bounds b = B;
        Burst("ice_shatter", b.center, b.size.y * 1.2f, Color.white, 0.6f, 0.5f, 1.6f, 4);
        Burst("ring", Feet + Vector3.up * 0.05f, b.size.x, Ice, 0.7f, 0.6f, 4f, -2, 0.35f);
        Burst("sun", b.center, b.size.y * 1.4f, new Color(0.6f, 0.9f, 1f, 0.7f), 0.5f, 0.7f, 1.3f, 3);
    }

    protected override void PlayDeath()
    {
        Bounds b = B;
        Burst("ice_shatter", b.center, b.size.y * 1.4f, Color.white, 0.7f, 0.8f, 2f, 5);
        Burst("gold_burst", b.center, b.size.y * 1.6f, new Color(0.8f, 0.95f, 1f), 0.6f, 0.8f, 1.6f, 6);
        Burst("ring", b.center, b.size.y * 0.5f, Ice, 0.8f, 1f, 6f, 4);
    }
}
