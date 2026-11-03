using UnityEngine;

public sealed class RedChainShockWave : MonoBehaviour
{
    [SerializeField] private SpriteData frames;
    [SerializeField] private SpriteManager spriteManager;
    [SerializeField, Min(0.1f)] private float lifetime = 1.5f;
    private float expiresAt;

    public void Play(Vector2 position, SpriteRenderer owner)
    {
        transform.position = position;
        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (owner != null) { renderer.sortingLayerID = owner.sortingLayerID; renderer.sortingOrder = owner.sortingOrder + 1; }
        expiresAt = Time.time + lifetime;
        gameObject.SetActive(true);
        spriteManager.ChangeState(frames);
    }

    private void Update() { if (Time.time >= expiresAt) gameObject.SetActive(false); }
    private void OnDisable() { if (spriteManager != null) spriteManager.Pause(); }
}
