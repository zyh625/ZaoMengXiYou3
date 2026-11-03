using UnityEngine;

// Visual only. The controller's single landing sample owns damage, never the lingering effect.
public sealed class RollingFurnaceShockwave : MonoBehaviour
{
    [SerializeField] private SpriteData frames;
    [SerializeField] private SpriteManager spriteManager;
    [SerializeField] private float nativeWidth = 1.7f;
    private float expires;
    public void Play(Vector2 position, float width)
    {
        transform.position = position; transform.localScale = new Vector3(width / nativeWidth, width / nativeWidth, 1f);
        gameObject.SetActive(true); spriteManager.ChangeState(frames); expires = Time.time + frames.sprites.Length / frames.fps;
    }
    private void Update() { if (Time.time >= expires) gameObject.SetActive(false); }
    private void OnDisable() { if (spriteManager != null) spriteManager.Pause(); }
}
