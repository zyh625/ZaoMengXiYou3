using System.Collections;
using UnityEngine;

//角色身上的通用特效，由 PlayerStats 自动挂上：
//· 升级：金色光柱从脚下升起，地面金环向外扩散，金星上飘，屏幕上方提示“XX 升到 N 级”。
//· 上场（换人）：脚下青色光环一闪，身上闪一下光，提示换上了谁。
public sealed class PlayerFx : MonoBehaviour
{
    private PlayerResources resources;
    private SpriteRenderer body;
    private static readonly string[] Names = { "悟空", "八戒", "沙僧", "唐僧" };
    private static readonly Color Gold = new(1f, 0.84f, 0.3f);

    private void Awake()
    {
        resources = GetComponent<PlayerResources>();
        PlayerController player = GetComponent<PlayerController>();
        if (player != null && player.BodySpriteManager != null) body = player.BodySpriteManager.GetComponent<SpriteRenderer>();
        if (body == null) body = GetComponentInChildren<SpriteRenderer>(true);
    }

    private void OnEnable()
    {
        if (resources != null) resources.LeveledUp += OnLevelUp;
        if (BattleRun.IsRunning || TrainingSession.Active) StartCoroutine(SwitchIn());//关卡开局的第一次启用不在战斗中，不播放
    }

    private void OnDisable()
    {
        if (resources != null) resources.LeveledUp -= OnLevelUp;
    }

    private bool Ready => body != null && isActiveAndEnabled;
    private Bounds B => body.bounds;

    private IEnumerator SwitchIn()
    {
        yield return null;//等角色站到换人后的位置
        if (!Ready) yield break;
        Bounds b = B;
        FxSpec ring = Fx.Make("ring", new Vector3(b.center.x, b.min.y + 0.05f, 0f), b.size.x * 0.8f, new Color(0.55f, 0.95f, 1f, 0.9f));
        ring.squashY = 0.35f; ring.life = 0.45f; ring.growTo = 2.6f; ring.fadeOut = 0.3f;
        Fx.SortLike(ref ring, body, -2);
        Fx.Spawn(ring);
        FxSpec flash = Fx.Make("sun", b.center, b.size.y * 1.1f, new Color(0.7f, 0.95f, 1f, 0.6f));
        flash.life = 0.3f; flash.fadeOut = 0.25f;
        Fx.SortLike(ref flash, body, 2);
        Fx.Spawn(flash);
    }

    private void OnLevelUp(int level)
    {
        if (!Ready || !(BattleRun.IsRunning || TrainingSession.Active)) return;
        Bounds b = B;
        Vector3 feet = new(b.center.x, b.min.y, 0f);
        //金色光柱：拉长的光晕，跟着角色
        FxSpec pillar = Fx.Make(null, new Vector3(0f, b.size.y * 0.55f, 0f) + (feet - transform.position), b.size.x * 1.1f, new Color(1f, 0.85f, 0.35f, 0.75f));
        pillar.sprite = FxTextures.Glow; pillar.follow = transform; pillar.squashY = 3.2f; pillar.life = 1.1f;
        pillar.growFrom = 0.5f; pillar.growTo = 1.15f; pillar.fadeIn = 0.1f; pillar.fadeOut = 0.5f;
        Fx.SortLike(ref pillar, body, -3);
        Fx.Spawn(pillar);
        //地面两道金环
        for (int i = 0; i < 2; i++)
        {
            FxSpec ring = Fx.Make("ring", feet - transform.position + Vector3.up * 0.05f, b.size.x * 0.6f, new Color(Gold.r, Gold.g, Gold.b, 1f - 0.35f * i));
            ring.follow = transform; ring.squashY = 0.35f; ring.life = 0.7f + 0.25f * i; ring.growTo = 3.2f + i; ring.fadeOut = 0.4f;
            Fx.SortLike(ref ring, body, -2);
            Fx.Spawn(ring);
        }
        //金星上飘
        FxSpec stars = Fx.Make("stars", b.center - transform.position, b.size.y * 0.9f, Gold);
        stars.follow = transform; stars.loop = true; stars.life = 1.2f; stars.drift = new Vector2(0f, 1.2f); stars.fadeOut = 0.5f;
        Fx.SortLike(ref stars, body, 3);
        Fx.Spawn(stars);
        FxSpec flash = Fx.Make("gold_burst", b.center, b.size.y * 1.4f, Color.white);
        flash.life = 0.35f; flash.growFrom = 0.6f; flash.growTo = 1.3f; flash.fadeOut = 0.25f;
        Fx.SortLike(ref flash, body, 4);
        Fx.Spawn(flash);
        string who = resources != null ? Names[Mathf.Clamp((int)resources.Id, 0, 3)] : "";
        EquipmentToast.Message($"{who} 升到 {level} 级！", Gold);
    }
}
