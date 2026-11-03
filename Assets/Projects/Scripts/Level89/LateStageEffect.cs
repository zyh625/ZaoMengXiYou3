using UnityEngine;

// Presentation only. Combat owns every damage event and its once/tick guards.
public sealed class LateStageEffect : MonoBehaviour
{
    [SerializeField] private SpriteData frames;
    [SerializeField] private SpriteManager spriteManager;
    [SerializeField] private SpriteRenderer visual;
    [SerializeField] private float nativeWidth = 1f;
    [SerializeField] private Vector2 placeholderAspect=Vector2.one;
    private static Sprite placeholder;
    private float remaining;
    private bool paused;
    public void Play(Vector2 at, float width, float duration, bool right, Color tint)
    {
        transform.position=at; transform.localScale=Vector3.one*(width/Mathf.Max(.01f,nativeWidth));
        remaining=duration;paused=false;visual.flipX=right; visual.color=tint;
        gameObject.SetActive(true);
        if(frames!=null&&frames.sprites!=null&&frames.sprites.Length>0)spriteManager.ChangeState(frames);
        else
        {
            if(placeholder==null)placeholder=Sprite.Create(Texture2D.whiteTexture,new Rect(0,0,2,2),new Vector2(.5f,.5f),2);
            visual.sprite=placeholder;visual.transform.localScale=new Vector3(placeholderAspect.x,placeholderAspect.y,1);
        }
    }
    public void SetPaused(bool value){if(value==paused)return;paused=value;if(value)spriteManager.Pause();else spriteManager.Resume();}
    private void Update(){if(paused)return;remaining-=Time.deltaTime;if(remaining<=0f)gameObject.SetActive(false);}
}
