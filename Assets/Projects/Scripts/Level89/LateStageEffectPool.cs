using UnityEngine;
using zmxy.GamePlay;

public sealed class LateStageEffectPool : MonoBehaviour
{
    [SerializeField] private LateStageEffect prefab;
    [SerializeField] private int prewarm=4;
    private readonly PrefabPool<LateStageEffect> pool=new();
    private Transform root,creation;
    private LateStageCombat owner;
    private readonly System.Collections.Generic.List<LateStageEffect> leased=new();
    private void Awake()=>owner=GetComponentInParent<LateStageCombat>();
    public LateStageEffect Play(Vector2 at,float width,float duration,bool right,Color tint)
    {
        if(root==null){root=new GameObject(name+"_WorldEffects").transform;creation=new GameObject("InactiveCreation").transform;
            creation.SetParent(root,false);creation.gameObject.SetActive(false);pool.Prewarm(prefab,prewarm,root,creation);}
        var fx=pool.Get(prefab,root,creation);fx.Play(at,width,duration,right,tint);if(!leased.Contains(fx))leased.Add(fx);return fx;
    }
    public void ReleaseAll(){pool.ReleaseAll();leased.Clear();}
    private void LateUpdate(){for(int i=leased.Count-1;i>=0;i--){var fx=leased[i];if(fx==null||!fx.gameObject.activeSelf)leased.RemoveAt(i);else fx.SetPaused(owner!=null&&owner.Frozen);}}
    private void OnDisable()=>ReleaseAll();
    private void OnDestroy(){if(root!=null)Destroy(root.gameObject);}
}
