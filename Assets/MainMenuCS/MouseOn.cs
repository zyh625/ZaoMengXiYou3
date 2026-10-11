using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MouseOn : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    Vector3 beginScale;
    [SerializeField] private Sprite normal, lighted;
    Image img;
    void Start()
    {
        img = GetComponent<Image>();
        beginScale = transform.localScale;
    }
    public void OnPointerEnter(PointerEventData e)
    {
        img.sprite = lighted;
        transform.localScale = beginScale * 1.1f;
    }
    public void OnPointerExit(PointerEventData e)
    {
        img.sprite=normal;
        transform.localScale=beginScale;
    }
}
