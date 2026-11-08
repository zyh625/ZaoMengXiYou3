using System.Collections.Generic;
using UnityEngine;

//Boss 掉在地上的装备：图标上下浮动，身后发出品质颜色的光。当前角色碰到就拾取；不会随时间消失。
//背包满了就留在地上，空出位置后再碰一次即可拾取。必须自己去捡，离开关卡时没捡的就没了。
public sealed class EquipmentPickup : MonoBehaviour
{
    private static readonly List<EquipmentPickup> active = new();
    private GoldManager owner;
    private EquipmentDatabase.Item def;
    private int rarity;
    private SpriteRenderer icon, glow, ring;
    private Vector3 ground;
    private float born, nextFullWarning;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetList() => active.Clear();

    public static void Spawn(GoldManager owner, Vector2 groundPoint, EquipmentDatabase.Item def, int rarity)
    {
        var go = new GameObject("EquipmentDrop_" + def.id);
        var pickup = go.AddComponent<EquipmentPickup>();
        pickup.owner = owner; pickup.def = def; pickup.rarity = rarity;
        pickup.ground = groundPoint;
        go.transform.position = groundPoint + Vector2.up * 0.6f;
        Color c = EquipmentRules.ItemColor(def, rarity);
        pickup.glow = Make(go.transform, "Glow", FxTextures.Glow, new Color(c.r, c.g, c.b, 0.75f), 40, 1.6f);
        pickup.ring = Make(go.transform, "Ring", FxTextures.Ring, new Color(c.r, c.g, c.b, 0.6f), 41, 1.1f);
        pickup.icon = Make(go.transform, "Icon", def.icon, Color.white, 42, 1f);
        if (def.icon != null)//图标统一缩放到约0.7个单位宽
        {
            float size = Mathf.Max(def.icon.bounds.size.x, def.icon.bounds.size.y);
            if (size > 0f) pickup.icon.transform.localScale = Vector3.one * (0.7f / size);
        }
        pickup.born = Time.time;
        active.Add(pickup);
    }

    private static SpriteRenderer Make(Transform parent, string name, Sprite sprite, Color color, int order, float scale)
    {
        var r = new GameObject(name).AddComponent<SpriteRenderer>();
        r.transform.SetParent(parent, false);
        r.transform.localScale = Vector3.one * scale;
        r.sprite = sprite; r.color = color; r.sortingOrder = order;
        return r;
    }

    private void Update()
    {
        float t = Time.time - born;
        transform.position = ground + Vector3.up * (0.6f + 0.12f * Mathf.Sin(t * 2.4f));
        float pulse = 0.5f + 0.5f * Mathf.Sin(t * 3f);
        glow.transform.localScale = Vector3.one * (1.5f + 0.25f * pulse);
        ring.transform.localScale = Vector3.one * (1.0f + 0.5f * ((t * 0.8f) % 1f));//光圈缓缓向外扩散
        Color rc = ring.color; rc.a = 0.6f * (1f - (t * 0.8f) % 1f); ring.color = rc;

        PlayerController player = owner != null ? owner.Collector : null;
        if (player == null) return;
        Collider2D body = player.GetComponent<Collider2D>();
        if (body == null) return;
        Bounds b = body.bounds;
        Vector2 p = transform.position;
        if (p.x < b.min.x - 0.35f || p.x > b.max.x + 0.35f || p.y < b.min.y - 0.35f || p.y > b.max.y + 0.35f) return;
        TryCollect();
    }

    private bool TryCollect()
    {
        SaveData data = GameSession.EnsureSession().EnsureRuntimeData();
        if (EquipmentService.Grant(data, def, rarity) == null)
        {
            if (Time.time >= nextFullWarning) { EquipmentToast.Message("背包已满，先出售或整理装备", Color.white); nextFullWarning = Time.time + 3f; }
            return false;
        }
        active.Remove(this);
        Destroy(gameObject);
        return true;
    }

    private void OnDestroy() => active.Remove(this);
}
