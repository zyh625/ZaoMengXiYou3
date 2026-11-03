using System;
using UnityEngine;

public sealed class LateStageDestructible : MonoBehaviour
{
    [SerializeField] private SpriteData frames;
    private Health hp;private float remaining;private Action<bool> lost;private bool live;
    public Health Health=>hp!=null?hp:hp=GetComponent<Health>();
    private void Awake(){hp=GetComponent<Health>();hp.Died+=OnDeath;}
    public void Activate(Vector2 at,float duration,Action<bool> callback)
    {transform.position=at;remaining=duration;lost=callback;live=true;gameObject.SetActive(true);Health.SetSuperArmor(this,true);GetComponentInChildren<SpriteManager>().ChangeState(frames);}
    public void Dismiss(){live=false;lost=null;gameObject.SetActive(false);}
    private void OnDeath(DamageInfo info)=>Expire(true);
    private void Update(){if(live&&(remaining-=Time.deltaTime)<=0)Expire(false);}
    private void Expire(bool broken){if(!live)return;live=false;var callback=lost;lost=null;gameObject.SetActive(false);callback?.Invoke(broken);}
    private void OnDestroy(){if(hp!=null)hp.Died-=OnDeath;}
    private void OnDisable(){if(hp!=null)hp.SetSuperArmor(this,false);}
}
