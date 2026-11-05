using UnityEngine;

// Visual only: every damage event is owned by Combat, never by a pooled effect.
public sealed class RedFurnaceLordEffect : MonoBehaviour
{
    [SerializeField] private SpriteData frames;
    [SerializeField] private SpriteManager spriteManager;
    [SerializeField] private SpriteRenderer visual;
    [SerializeField] private float nativeWidth = 2.08f;
    private float remaining;
    public void Play(Vector2 position, float width, bool right, float duration)
    {
        transform.position = position;
        transform.localScale = Vector3.one * (width / Mathf.Max(BalanceRuntime.Number(this, "RedFurnaceLordEffect.Play.1", 0.01f), nativeWidth));
        visual.flipX = right; // Supplied effect sheets point to image-left.
        remaining = duration;
        gameObject.SetActive(true); spriteManager.ChangeState(frames);
    }
    private void Update()
    {
        remaining -= Time.deltaTime;
        if (remaining <= 0f) gameObject.SetActive(false);
    }
}
