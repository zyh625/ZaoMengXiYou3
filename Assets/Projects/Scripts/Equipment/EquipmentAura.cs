using System.Collections.Generic;
using UnityEngine;

//用特效体现装备品质，不改角色帧图：
//· 防具品质 → 角色身后的光环（精良及以上显示，传说额外有上升的光点）
//· 武器品质 → 命中敌人时的火花颜色（优秀及以上显示，暴击更大）
public sealed class EquipmentAura : MonoBehaviour
{
    private PlayerStats stats;
    private SpriteRenderer body, glow;
    private readonly List<SpriteRenderer> motes = new();
    private float[] motePhase;

    private void Awake()
    {
        stats = GetComponent<PlayerStats>();
        PlayerController player = GetComponent<PlayerController>();
        if (player != null && player.BodySpriteManager != null) body = player.BodySpriteManager.GetComponent<SpriteRenderer>();
        if (body == null) body = GetComponentInChildren<SpriteRenderer>(true);
        var go = new GameObject("EquipmentAura");
        go.transform.SetParent(transform, false);
        glow = go.AddComponent<SpriteRenderer>();
        glow.sprite = FxTextures.Glow;
        glow.enabled = false;
        motePhase = new float[6];
        for (int i = 0; i < motePhase.Length; i++)
        {
            var mote = new GameObject("Mote").AddComponent<SpriteRenderer>();
            mote.transform.SetParent(go.transform, false);
            mote.sprite = FxTextures.Spark;
            mote.enabled = false;
            motes.Add(mote);
            motePhase[i] = i / (float)motePhase.Length;
        }
    }

    private void LateUpdate()
    {
        int rarity = stats != null ? stats.ArmorRarity : -1;
        bool show = rarity >= (int)EquipRarity.Superior && body != null && body.enabled && body.sprite != null;
        glow.enabled = show;
        bool legendary = show && rarity >= (int)EquipRarity.Legendary;
        for (int i = 0; i < motes.Count; i++) motes[i].enabled = legendary;
        if (!show) return;

        Bounds b = body.bounds;
        Color c = EquipmentRules.RarityColor(rarity);
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 2.2f);
        float alpha = rarity == (int)EquipRarity.Superior ? 0.22f : rarity == (int)EquipRarity.Epic ? 0.30f : 0.38f;
        glow.transform.position = new Vector3(b.center.x, b.center.y, transform.position.z);
        float size = Mathf.Max(b.size.x, b.size.y) * (1.25f + 0.06f * pulse);
        glow.transform.localScale = Vector3.one * size / transform.lossyScale.x;
        glow.color = new Color(c.r, c.g, c.b, alpha * (0.75f + 0.25f * pulse));
        glow.sortingLayerID = body.sortingLayerID;
        glow.sortingOrder = body.sortingOrder - 2;//在本体和无双残影后面

        if (!legendary) return;
        for (int i = 0; i < motes.Count; i++)//传说：绕身体缓缓上升的光点
        {
            float t = (Time.time * 0.45f + motePhase[i]) % 1f;
            float x = Mathf.Sin((t + i * 0.37f) * Mathf.PI * 2f) * b.extents.x * 0.9f;
            float y = Mathf.Lerp(-b.extents.y * 0.9f, b.extents.y, t);
            SpriteRenderer m = motes[i];
            m.transform.position = new Vector3(b.center.x + x, b.center.y + y, transform.position.z);
            m.transform.localScale = Vector3.one * (0.18f + 0.08f * Mathf.Sin(t * Mathf.PI)) / transform.lossyScale.x;
            m.color = new Color(c.r, c.g, c.b, Mathf.Sin(t * Mathf.PI) * 0.9f);
            m.sortingLayerID = body.sortingLayerID;
            m.sortingOrder = body.sortingOrder + 1;
        }
    }

    //---------- 武器命中火花 ----------
    private sealed class Spark { public SpriteRenderer core, ring; public float age, life, size; }
    private static readonly List<Spark> sparks = new();
    private static Transform sparkRoot;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Register()
    {
        sparks.Clear();
        sparkRoot = null;
        Health.AnyDamageReceived -= OnHit;
        Health.AnyDamageReceived += OnHit;
    }

    private static void OnHit(Health target, DamageInfo damage, float amount)
    {
        if (target == null || damage.Source == null || target.GetComponent<PlayerStats>() != null) return;
        PlayerStats attacker = damage.Source.GetComponentInParent<PlayerStats>();
        if (attacker == null || attacker.WeaponRarity < (int)EquipRarity.Fine) return;
        Collider2D col = target.GetComponent<Collider2D>();
        Vector2 at = col != null ? (Vector2)col.bounds.center + Random.insideUnitCircle * col.bounds.extents.y * 0.3f : (Vector2)target.transform.position;
        SpriteRenderer targetRenderer = target.GetComponentInChildren<SpriteRenderer>();
        Color c = EquipmentRules.RarityColor(attacker.WeaponRarity);
        float size = (0.7f + 0.12f * attacker.WeaponRarity) * (damage.IsCritical ? 1.6f : 1f);
        Spark s = GetSpark();
        s.age = 0f; s.life = damage.IsCritical ? 0.3f : 0.22f; s.size = size;
        s.core.transform.position = s.ring.transform.position = new Vector3(at.x, at.y, 0f);
        s.core.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 90f));
        s.core.color = damage.IsCritical ? Color.white : c;
        s.ring.color = c;
        int layer = targetRenderer != null ? targetRenderer.sortingLayerID : 0;
        int order = targetRenderer != null ? targetRenderer.sortingOrder + 20 : 100;
        s.core.sortingLayerID = s.ring.sortingLayerID = layer;
        s.ring.sortingOrder = order; s.core.sortingOrder = order + 1;
        s.core.gameObject.SetActive(true);
    }

    private static Spark GetSpark()
    {
        foreach (Spark s in sparks) if (s.core != null && !s.core.gameObject.activeSelf) return s;
        if (sparkRoot == null)
        {
            sparks.RemoveAll(s => s.core == null);
            sparkRoot = new GameObject("EquipmentHitSparks").AddComponent<SparkDriver>().transform;
        }
        var core = new GameObject("Spark").AddComponent<SpriteRenderer>();
        core.transform.SetParent(sparkRoot, false);
        core.sprite = FxTextures.Spark;
        var ring = new GameObject("Ring").AddComponent<SpriteRenderer>();
        ring.transform.SetParent(core.transform, false);
        ring.sprite = FxTextures.Ring;
        var spark = new Spark { core = core, ring = ring };
        sparks.Add(spark);
        return spark;
    }

    private sealed class SparkDriver : MonoBehaviour//场景切换时跟着场景销毁，下次命中再重建
    {
        private void LateUpdate()
        {
            foreach (Spark s in sparks)
            {
                if (s.core == null || !s.core.gameObject.activeSelf) continue;
                s.age += Time.deltaTime;
                float t = s.age / s.life;
                if (t >= 1f) { s.core.gameObject.SetActive(false); continue; }
                float a = 1f - t * t;
                s.core.transform.localScale = Vector3.one * s.size * (0.6f + 0.5f * t);
                s.ring.transform.localScale = Vector3.one * (0.6f + 1.2f * t);
                Color cc = s.core.color; cc.a = a; s.core.color = cc;
                Color rc = s.ring.color; rc.a = a * 0.8f; s.ring.color = rc;
            }
        }
    }
}

//代码生成的特效贴图，不需要额外美术资源
public static class FxTextures
{
    private static Sprite glow, spark, ring;
    public static Sprite Glow => glow != null ? glow : glow = Make(128, (x, y) => { float d = Mathf.Sqrt(x * x + y * y); return Mathf.Pow(Mathf.Clamp01(1f - d), 2.2f); });
    public static Sprite Spark => spark != null ? spark : spark = Make(64, (x, y) =>
    {
        float d = Mathf.Sqrt(x * x + y * y);
        float star = Mathf.Max(Mathf.Clamp01(1f - Mathf.Abs(x) * 9f) * Mathf.Clamp01(1f - Mathf.Abs(y)), Mathf.Clamp01(1f - Mathf.Abs(y) * 9f) * Mathf.Clamp01(1f - Mathf.Abs(x)));
        return Mathf.Clamp01(star + Mathf.Pow(Mathf.Clamp01(1f - d * 2.2f), 2f));
    });
    public static Sprite Ring => ring != null ? ring : ring = Make(64, (x, y) => { float d = Mathf.Sqrt(x * x + y * y); return Mathf.Clamp01(1f - Mathf.Abs(d - 0.8f) * 9f); });

    private static Sprite Make(int size, System.Func<float, float, float> alpha)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float a = alpha((x + 0.5f) / size * 2f - 1f, (y + 0.5f) / size * 2f - 1f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);//1个世界单位宽
    }
}
