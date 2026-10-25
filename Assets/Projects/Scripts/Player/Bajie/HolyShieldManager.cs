using UnityEngine;

public class HolyShieldManager : MonoBehaviour
{
    [SerializeField] private SpriteData shieldLight, shieldMiss;
    [SerializeField] private float lightDuration;
    private SpriteManager SM;
    private void Awake()=>SM.GetComponentInChildren<SpriteManager>();
    private float lastShield = -1f;
    private bool ShieldLight = false, ShieldMiss = false;
    private void OnEnable()
    {
        SM.gameObject.SetActive(false);
        ShieldLight = false;
        ShieldMiss = false;
        lastShield = -1f;
    }
    public void OpenShield()//按下技能一
    {
        SM.gameObject.SetActive(true);
        SM.ChangeState(shieldLight);
        lastShield = Time.time;
        ShieldLight = true;
    }
    private void FixedUpdate()
    {
        if (ShieldLight&&Time.time-lastShield>lightDuration)
        {
            ShieldLight=false;
            lastShield = -1f;
            SM.ChangeState(shieldMiss);
        }
        else if (ShieldMiss && SM.IsFinished)
        {
            ShieldMiss = false;
            SM.gameObject.SetActive(false);
        }
    }

}
