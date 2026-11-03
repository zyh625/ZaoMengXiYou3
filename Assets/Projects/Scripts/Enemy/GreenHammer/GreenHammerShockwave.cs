using UnityEngine;

//青玉锤卒专属翡翠冲击波；表现与一次性触发判定分开。
public sealed class GreenHammerShockwave : MonoBehaviour
{
    [SerializeField] private SpriteData frames;
    [SerializeField] private SpriteManager spriteManager;
    [SerializeField] private SpriteRenderer visual;
    [SerializeField] private GreenHammerAttackBox damageBox;
    [SerializeField] private float nativeWidth=2f,lifetime=1f;
    private float expiresAt;
    public void Play(EnemyCombat source,Vector2 position,float width,bool damage,float scale)
    {
        transform.position=position;visual.transform.localScale=new Vector3(width/nativeWidth,1f,1f);
        visual.flipX=source!=null && source.AttackFaceRight; //原图向左，向右出招时只翻转表现。
        SpriteRenderer renderer=source!=null?source.GetComponentInChildren<SpriteRenderer>(true):null;
        if(renderer!=null){visual.sortingLayerID=renderer.sortingLayerID;visual.sortingOrder=renderer.sortingOrder+1;}
        damageBox.BindWave(source,scale);gameObject.SetActive(true);spriteManager.ChangeState(frames);expiresAt=Time.time+lifetime;
        BoxCollider2D box=damageBox.GetComponent<BoxCollider2D>();box.enabled=damage;
        if(damage){Physics2D.SyncTransforms();damageBox.SampleNow();}
        box.enabled=false; //每段只在生成时采样，特效留存期间不再伤人。
    }
    private void Update(){if(Time.time>=expiresAt)gameObject.SetActive(false);}
    private void OnDisable(){spriteManager.Pause();damageBox.GetComponent<BoxCollider2D>().enabled=false;}
}
