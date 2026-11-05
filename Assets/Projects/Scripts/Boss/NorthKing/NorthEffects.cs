using UnityEngine;

public sealed class NorthEffects : MonoBehaviour
{
    [SerializeField] private NorthKingRib ribPrefab;
    [SerializeField] private NorthKingRain rainPrefab;
    [SerializeField] private NorthKingShield shieldPrefab;
    private NorthKingShield shield;
    public int RibsEmitted {get;private set;}
    public int RainEmitted {get;private set;}
    public void Fan(NorthCombat source,Vector2 from,bool right,int count)
    {
        for(int i=0;i<count;i++)
        {
            float angle=Mathf.Lerp(count==3?-BalanceRuntime.Number(this, "NorthEffects.Fan.1", 18f):-BalanceRuntime.Number(this, "NorthEffects.Fan.2", 24f),count==3?BalanceRuntime.Number(this, "NorthEffects.Fan.3", 18f):BalanceRuntime.Number(this, "NorthEffects.Fan.4", 24f),i/(float)(count-1))*Mathf.Deg2Rad;
            NorthEffectPool.GetOrCreate().Rib(ribPrefab,source,from,new Vector2(Mathf.Cos(angle)*(right?1f:-1f),Mathf.Sin(angle)));RibsEmitted++;
        }
    }
    public void Rain(NorthCombat source,Vector2 arena)
    {
        for(int i=0;i<6;i++)
        {
            float x=Mathf.Lerp(arena.x+1f,arena.y-1f,(i+BalanceRuntime.Number(this, "NorthEffects.Rain.1", 0.5f))/BalanceRuntime.Number(this, "NorthEffects.Rain.2", 6f));RaycastHit2D ground=Physics2D.Raycast(new Vector2(x,source.transform.position.y+BalanceRuntime.Number(this, "NorthEffects.Rain.3", 15f)),Vector2.down,BalanceRuntime.Number(this, "NorthEffects.Rain.4", 40f),1<<6);
            if(ground.collider==null){x=source.transform.position.x;ground=Physics2D.Raycast((Vector2)source.transform.position+Vector2.up*BalanceRuntime.Number(this, "NorthEffects.Rain.5", 2f),Vector2.down,BalanceRuntime.Number(this, "NorthEffects.Rain.6", 40f),1<<6);}
            NorthEffectPool.GetOrCreate().Rain(rainPrefab,source,new Vector2(x,ground.collider!=null?ground.point.y:source.transform.position.y));RainEmitted++;
        }
    }
    public void ShowShield(NorthCombat source){HideShield();shield=NorthEffectPool.GetOrCreate().Shield(shieldPrefab,source);}
    public void HideShield(){if(shield!=null)shield.gameObject.SetActive(false);shield=null;}
    public void ResetCounters(){RibsEmitted=RainEmitted=0;}
    private void OnDisable()=>HideShield();
}
