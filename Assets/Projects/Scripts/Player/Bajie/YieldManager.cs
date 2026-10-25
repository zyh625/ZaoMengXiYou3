using UnityEngine;

public class YieldManager : MonoBehaviour
{
    [SerializeField] private SpriteData yield;
    [SerializeField] private float duration;
    private SpriteManager SM;
    private void Awake() => SM.GetComponentInChildren<SpriteManager>();
    private float lastYield= -1f;
    private bool Yield = false;
    private void OnEnable()
    {
        SM.gameObject.SetActive(false);
        Yield = false;
        lastYield = -1f;
    }
    public void OpenShield()//按下技能一
    {
        SM.gameObject.SetActive(true);
        SM.ChangeState(yield);
        lastYield = Time.time;
        Yield = true;
    }
    private void FixedUpdate()
    {
        if (Yield && Time.time - lastYield > duration)
        {
            Yield = false;
            lastYield = -1f;
            SM.gameObject.SetActive(false);
        }
    }
}
