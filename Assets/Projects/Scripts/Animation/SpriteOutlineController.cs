using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class SpriteOutlineController : MonoBehaviour
{
    private static readonly int ColorId =
        Shader.PropertyToID("_OutlineColor");
    private static readonly int WidthId =
        Shader.PropertyToID("_OutlineWidth");

    private SpriteRenderer spriteRenderer;
    private MaterialPropertyBlock propertyBlock;

    private void Awake()
    {
        EnsureInitialized();
    }

    private void EnsureInitialized()
    {
        // Another component's OnEnable can call us before our Awake runs.
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
        propertyBlock ??= new MaterialPropertyBlock();
    }

    public void SetOutline(bool active, float width = 2f)
    {
        EnsureInitialized();
        spriteRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetFloat(
            WidthId, active ? Mathf.Clamp(width, 0f, 8f) : 0f);
        spriteRenderer.SetPropertyBlock(propertyBlock);
    }

    public void SetOutlineColor(Color color)
    {
        EnsureInitialized();
        spriteRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(ColorId, color);
        spriteRenderer.SetPropertyBlock(propertyBlock);
    }
}
