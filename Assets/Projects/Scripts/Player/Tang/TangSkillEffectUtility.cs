using UnityEngine;

//唐僧附着式技能共用显示与图片范围计算，独立特效不再各自推进动画时钟。
internal static class TangSkillEffectUtility
{
    internal static void Resolve(GameObject effect, ref SpriteManager sm, ref SpriteRenderer renderer)
    {
        if (sm == null) sm = effect.GetComponent<SpriteManager>();
        if (renderer == null) renderer = effect.GetComponent<SpriteRenderer>();
    }

    internal static void Place(Transform effect, Transform caster, Vector2 offset, bool right)
    {
        Vector3 position = effect.localPosition;//只镜像技能配置的位置，不改人物自己的Transform。
        position.x = right ? offset.x : -offset.x;
        position.y = offset.y;
        effect.localPosition = position;
        SpriteRenderer renderer = effect.GetComponent<SpriteRenderer>();
        Transform visual = caster != null ? caster.Find("Visual") : null;
        SpriteRenderer body = visual != null ? visual.GetComponent<SpriteRenderer>() : null;
        if (renderer == null) return;
        renderer.flipX = !right;
        if (body == null) return;
        renderer.sortingLayerID = body.sortingLayerID;
        renderer.sortingOrder = body.sortingOrder + 1;
    }

    internal static void Show(SpriteManager sm, SpriteRenderer renderer, SpriteData animation, int frame)
    {
        if (sm != null) sm.Pause();//帧索引完全跟随本体，SM.Update不能在同帧把特效推进到下一张。
        if (renderer == null) return;
        bool valid = animation != null && animation.sprites != null && frame >= 0 && frame < animation.sprites.Length;
        renderer.enabled = valid;
        if (valid) renderer.sprite = animation.sprites[frame];
    }

    internal static void Hide(SpriteManager sm, SpriteRenderer renderer)
    {
        if (sm != null) sm.Pause();
        if (renderer != null) renderer.enabled = false;
    }

    //每个Rect来自该帧实际非透明像素，相对Sprite pivot、以本地单位表示，等大裁框不会扩大早期攻击范围。
    internal static bool TryBounds(Transform effect, SpriteRenderer renderer, Rect[] frameBounds, int frame, out Bounds bounds)
    {
        bounds = default;
        if (renderer == null || frameBounds == null || frame < 0 || frame >= frameBounds.Length) return false;
        Rect rect = frameBounds[frame];
        if (rect.width <= 0f || rect.height <= 0f) return false;
        float x1 = renderer.flipX ? -rect.xMax : rect.xMin;
        float x2 = renderer.flipX ? -rect.xMin : rect.xMax;
        float y1 = renderer.flipY ? -rect.yMax : rect.yMin;
        float y2 = renderer.flipY ? -rect.yMin : rect.yMax;
        bounds = new Bounds(effect.TransformPoint(new Vector3(x1, y1, 0f)), Vector3.zero);
        bounds.Encapsulate(effect.TransformPoint(new Vector3(x1, y2, 0f)));
        bounds.Encapsulate(effect.TransformPoint(new Vector3(x2, y1, 0f)));
        bounds.Encapsulate(effect.TransformPoint(new Vector3(x2, y2, 0f)));
        Vector3 size = bounds.size;//2D碰撞箱Z厚度通常为0，范围保留少量厚度以兼容不同显示层的对象。
        size.z = Mathf.Max(size.z, 1f);
        bounds.size = size;
        return true;
    }

    internal static bool Touches(EnemyMovement enemy, Bounds area)
        => enemy != null && enemy.BodyCollider != null && area.Intersects(enemy.BodyCollider.bounds);
}
