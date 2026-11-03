using UnityEngine;

//Boss持有池，护珠固定三个槽；全部借出时不会再扩容幻影。
public sealed class WestEffectPool : MonoBehaviour
{
    [SerializeField] private WestKingPearl pearlPrefab;
    [SerializeField] private WestKingFlame flamePrefab;
    [SerializeField] private WestKingOrb orbPrefab;
    private readonly PrefabPool<WestKingPearl> pearls=new();
    private readonly PrefabPool<WestKingFlame> flames=new();
    private readonly WestKingOrb[] orbs=new WestKingOrb[3];
    private Transform creation,effectsRoot;
    private WestCombat owner;
    public WestKingOrb[] Orbs=>orbs;
    private void Prepare()
    {
        if(creation!=null)return;
        owner=GetComponent<WestCombat>();
        effectsRoot=new GameObject("WestKingEffects").transform; //世界空间池，不跟随Boss移动。
        GameObject inactive=new("WestKingInactiveCreation");inactive.transform.SetParent(effectsRoot,false);inactive.SetActive(false);creation=inactive.transform;
        pearls.Prewarm(pearlPrefab,2,effectsRoot,creation);flames.Prewarm(flamePrefab,3,effectsRoot,creation);
        for(int i=0;i<3;i++)
        {
            orbs[i]=Instantiate(orbPrefab,creation);orbs[i].gameObject.SetActive(false);orbs[i].transform.SetParent(effectsRoot,false);
        }
    }
    public WestKingPearl Pearl(Vector2 from,bool orbit)
    {
        Prepare();WestKingPearl pearl=pearls.Get(pearlPrefab,effectsRoot,creation);pearl.Launch(owner,from,orbit);return pearl;
    }
    public WestKingFlame Flame(Vector2 from,bool right,bool pulse=false,float radius=1.2f)
    {
        Prepare();WestKingFlame flame=flames.Get(flamePrefab,effectsRoot,creation);flame.Launch(owner,from,right,pulse,radius);return flame;
    }
    public void SpawnOrbs(Vector2 limits,float floor)
    {
        Prepare();
        for(int i=0;i<3;i++)
        {
            Vector2 position=new(Mathf.Lerp(limits.x+1.5f,limits.y-1.5f,(i+0.5f)/3f),floor+Random.Range(2.6f,4.2f));
            orbs[i].Spawn(owner,position);
        }
    }
    public void ClearOrbs(){foreach(WestKingOrb orb in orbs)if(orb!=null)orb.gameObject.SetActive(false);}
    public void ReleaseAll(){pearls.ReleaseAll();flames.ReleaseAll();ClearOrbs();}
    private void OnDisable()=>ReleaseAll();
    private void OnDestroy(){if(effectsRoot!=null)Destroy(effectsRoot.gameObject);}
}
