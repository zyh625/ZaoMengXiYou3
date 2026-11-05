using UnityEngine;

public sealed class RedChainShockWaveEmitter : MonoBehaviour
{
    [SerializeField] private RedChainShockWave prefab;
    public void Emit(RedChainCombat source)
    {
        Collider2D body = source.GetComponent<Collider2D>();
        float feetY = body != null ? body.bounds.min.y : source.transform.position.y;
        Vector2 position = new(source.transform.position.x + (source.AttackFaceRight ? BalanceRuntime.Number(this, "RedChainShockWaveEmitter.Emit.1", 3f) : -BalanceRuntime.Number(this, "RedChainShockWaveEmitter.Emit.2", 3f)), feetY);
        RedChainShockWavePool.GetOrCreate().Spawn(prefab, position, source.GetComponentInChildren<SpriteRenderer>(true));
    }
}
