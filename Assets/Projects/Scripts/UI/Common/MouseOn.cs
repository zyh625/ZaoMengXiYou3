using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MouseOn : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
    IPointerClickHandler
{
    Vector3 beginScale;
    [SerializeField] private Sprite normal, lighted;
    [SerializeField] private Image levelName;
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
        if(levelName!=null)
            levelName.gameObject.SetActive(true);
    }
    public void OnPointerExit(PointerEventData e)
    {
        Return();
    }
    public void OnPointerClick(PointerEventData e)
    {
        Return();
    }
    public void Return()
    {
        img.sprite = normal;
        transform.localScale = beginScale;
        if (levelName != null)
            levelName.gameObject.SetActive(false);
    }
}
