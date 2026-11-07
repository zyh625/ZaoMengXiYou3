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
        transform.position = (from+to)/BalanceRuntime.Number(this, "RedBoomRay.Warn.1", 2f);
        transform.localScale = Vector3.one;
        Vector2 line = to-from;
        visual.transform.localScale = new Vector3(line.magnitude/nativeWidth,1f,1f);
        visual.flipX = line.x>0f; //原图射线朝左。
        //斜向射线：朝右时按射线方向旋转，朝左时按反方向旋转（图片本身朝左）
        Vector2 axis = line.x>0f ? line : -line;
        transform.rotation = Quaternion.Euler(0f,0f,Mathf.Atan2(axis.y,axis.x)*Mathf.Rad2Deg);
        if (source != null)
        {
            visual.sortingLayerID=source.sortingLayerID;visual.sortingOrder=source.sortingOrder+2;
            warningLine.sortingLayerID=source.sortingLayerID;warningLine.sortingOrder=source.sortingOrder+1;
        }
        warningLine.SetPosition(0,from);warningLine.SetPosition(1,to);
        expiresAt=Time.time+BalanceRuntime.Number(this, "RedBoomRay.Warn.2", 3f);
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
