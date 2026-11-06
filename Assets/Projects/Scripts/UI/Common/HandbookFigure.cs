using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

//图鉴里可点击的人物：鼠标悬停时人物轮廓发出黄光，点击时通知图鉴打开对应介绍页。
[RequireComponent(typeof(Image))]
public sealed class HandbookFigure : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] private Shadow[] glow = System.Array.Empty<Shadow>();//挂在人物Image上的描边，沿透明轮廓发光
    public event System.Action Clicked;

    private void OnEnable() => SetGlow(false);
    private void OnDisable() => SetGlow(false);
    public void OnPointerEnter(PointerEventData eventData) => SetGlow(true);
    public void OnPointerExit(PointerEventData eventData) => SetGlow(false);

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        SetGlow(false);
        Clicked?.Invoke();
    }

    private void SetGlow(bool on)
    {
        foreach (Shadow effect in glow)
            if (effect != null) effect.enabled = on;
    }
}
