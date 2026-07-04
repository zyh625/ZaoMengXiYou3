using UnityEngine;
using UnityEngine.UI;
public class GameManager : MonoBehaviour
{
    public enum UIAnchor
    {
        LeftTop,
        RightTop,
        LeftBottom,
        RightBottom,
        Center
    }
    const float DESIGN_WIDTH = 800f;//初始宽度
    const float DESIGN_HEIGHT = 600f;//初始高度
    public Canvas canvas;
    public Sprite bg;
    public Sprite row;
    public Sprite col;
    public Sprite brid;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }
    public RectTransform CreateImage(
    Sprite sprite,
    Vector2 size,
    UIAnchor anchor,
    float offsetX,
    float offsetY,
    bool scaleX = true,
    bool scaleY = true)
    {
        GameObject obj = new GameObject(sprite.name);

        obj.transform.SetParent(canvas.transform, false);

        Image img = obj.AddComponent<Image>();
        img.sprite = sprite;

        RectTransform rt = img.rectTransform;

        rt.sizeDelta = size;

        Vector2 anchorPos = Vector2.zero;

        switch (anchor)
        {
            case UIAnchor.LeftTop:
                anchorPos = new Vector2(0, 1);
                break;

            case UIAnchor.RightTop:
                anchorPos = new Vector2(1, 1);
                break;

            case UIAnchor.LeftBottom:
                anchorPos = new Vector2(0, 0);
                break;

            case UIAnchor.RightBottom:
                anchorPos = new Vector2(1, 0);
                break;

            case UIAnchor.Center:
                anchorPos = new Vector2(0.5f, 0.5f);
                break;
        }

        rt.anchorMin = anchorPos;
        rt.anchorMax = anchorPos;
        rt.pivot = anchorPos;

        RectTransform canvasRect =
            canvas.GetComponent<RectTransform>();

        float sx = canvasRect.rect.width / DESIGN_WIDTH;
        float sy = canvasRect.rect.height / DESIGN_HEIGHT;

        float x = scaleX ? offsetX * sx : offsetX;
        float y = scaleY ? offsetY * sy : offsetY;

        rt.anchoredPosition = new Vector2(x, y);

        return rt;
    }
}
