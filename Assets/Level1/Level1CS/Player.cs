using UnityEngine;

//第一关旧脚本使用的玩家桥：保留旧的静态接口，实际转发到新 PlayerManager / Health / PlayerResources。
//不再承担移动、攻击和 UI，角色控制完全交给 PlayerSystem。
public static class Player
{
    public static Transform tran { get; private set; }
    public static Collider2D col { get; private set; }
    public static Collider2D box { get; private set; }
    public static Collider2D box1 { get; private set; }
    public static float HalfSr { get; private set; }
    public static bool attackedDirect { get; set; }
    public static float attack { get; set; } = 10f;

    public static PlayerManager Manager { get; private set; }
    public static PlayerController Current => Manager != null ? Manager.CurrentPlayer : null;

    public static void Bind(PlayerManager manager)
    {
        if (Manager != null) Manager.CurrentPlayerChanged -= OnPlayerChanged;
        Manager = manager;
        if (Manager != null) Manager.CurrentPlayerChanged += OnPlayerChanged;
        Refresh();
    }

    private static void OnPlayerChanged(PlayerController player) => Refresh();

    public static void Refresh()
    {
        PlayerController player = Current;
        if (player == null)
        {
            tran = null;
            col = box = box1 = null;
            HalfSr = 0f;
            return;
        }
        tran = player.transform;
        col = player.GetComponent<Collider2D>();
        if (col == null) col = player.GetComponentInChildren<Collider2D>();
        box = col;
        box1 = col;
        SpriteRenderer renderer = player.GetComponentInChildren<SpriteRenderer>();
        HalfSr = renderer != null ? renderer.bounds.extents.x : (col != null ? col.bounds.extents.x : 0.5f);
    }

    public static void TakeDamage(float amount, Transform source = null, bool knockback = true)
    {
        Health health = Current != null ? Current.GetComponent<Health>() : null;
        if (health == null || health.Dead) return;
        health.TakeDamage(new DamageInfo(source, amount, knockback));
    }

    public static void UpdateExp(float amount)
    {
        PlayerResources resources = Current != null ? Current.GetComponent<PlayerResources>() : null;
        if (resources != null) resources.AddExperience(amount);
    }
}
