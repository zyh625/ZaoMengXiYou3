using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class SpriteOutlineController : MonoBehaviour
{
    private static readonly int ColorId =
        Shader.PropertyToID("_OutlineColor");
    private static readonly int WidthId =
        Shader.PropertyToID("_OutlineWidth");
    private static readonly int TexelSizeId =
        Shader.PropertyToID("_OutlineTexelSize");//使用自定义属性提供纹理像素大小，兼容URP 2D材质属性声明。

    private SpriteRenderer spriteRenderer;
    private MaterialPropertyBlock propertyBlock;
    private Texture currentTexture;//同一张精灵表切帧不重复写入，切换动作或图集后才更新像素尺寸。

    private void Awake()
    {
        EnsureInitialized();
        UpdateTextureSize();
    }

    private void OnEnable() => UpdateTextureSize();//对象池重新启用时同步当前图片，不创建新的材质实例。

    private void LateUpdate() => UpdateTextureSize();//SpriteManager在Update换帧之后读取实际纹理，保持描边宽度一致。

    private void UpdateTextureSize()
    {
        EnsureInitialized();
        Texture texture = spriteRenderer.sprite != null ? spriteRenderer.sprite.texture : null;
        if (texture == currentTexture) return;
        currentTexture = texture;
        spriteRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetVector(TexelSizeId, texture != null
            ? new Vector4(1f / texture.width, 1f / texture.height, texture.width, texture.height)
            : Vector4.zero);
        spriteRenderer.SetPropertyBlock(propertyBlock);
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
