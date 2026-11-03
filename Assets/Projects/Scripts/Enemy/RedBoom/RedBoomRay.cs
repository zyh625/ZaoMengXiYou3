using UnityEngine;

//纯表现：预警线与发射帧动画没有碰撞/伤害回调。
public sealed class RedBoomRay : MonoBehaviour
{
    [SerializeField] private SpriteData flash;
    [SerializeField] private SpriteManager spriteManager;
    [SerializeField] private SpriteRenderer visual;
    [SerializeField] private LineRenderer warningLine;
    [SerializeField] private float nativeWidth = 2f;
    private float expiresAt;
    public bool IsWarning { get; private set; }
    public void Warn(Vector2 from, Vector2 to, SpriteRenderer source)
    {
        transform.position = (from+to)/2f;
        transform.localScale = Vector3.one;
        visual.transform.localScale = new Vector3(Mathf.Abs(to.x-from.x)/nativeWidth,1f,1f);
        visual.flipX = to.x>from.x; //原图射线朝左。
        if (source != null)
        {
            visual.sortingLayerID=source.sortingLayerID;visual.sortingOrder=source.sortingOrder+2;
            warningLine.sortingLayerID=source.sortingLayerID;warningLine.sortingOrder=source.sortingOrder+1;
        }
        warningLine.SetPosition(0,from);warningLine.SetPosition(1,to);
        expiresAt=Time.time+3f;
        IsWarning=true;
        gameObject.SetActive(true);
        visual.enabled=false;warningLine.enabled=true;
    }
    public void Flash()
    {
        if (!gameObject.activeSelf) return;
        IsWarning=false;warningLine.enabled=false;visual.enabled=true;
        spriteManager.ChangeState(flash);
        expiresAt=Time.time+flash.sprites.Length/Mathf.Max(1f,flash.fps);
    }
    private void Update() { if (Time.time>=expiresAt) Cancel(); }
    public void Cancel() { if (gameObject.activeSelf) gameObject.SetActive(false); }
    private void OnDisable() { IsWarning=false;warningLine.enabled=false;visual.enabled=false;spriteManager.Pause(); }
}
