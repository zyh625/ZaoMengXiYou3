using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

//运行时构建常驻控制面板，按钮不连接存档、金币或关卡解锁。
public sealed class TrainingHUD : MonoBehaviour
{
    [SerializeField] private TrainingManager manager;
    [SerializeField] private TrainingHitboxDebug hitboxes;
    private TMP_Text nameText,statsText,playerText;
    private Button resources,cooldown,god,dummy,boxes,ai;
    private readonly StringBuilder text=new(512);
    private float refreshAt;
    private static readonly Color ink=new(0.07f,0.11f,0.16f,0.95f),accent=new(0.12f,0.55f,0.46f,1f);
    private RectTransform Panel(string name,Vector2 anchor,Vector2 position,Vector2 size)
    {
        GameObject go=new(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(transform,false);RectTransform r=go.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=anchor;r.pivot=anchor;r.anchoredPosition=position;r.sizeDelta=size;go.GetComponent<Image>().color=ink;return r;
    }
    private TMP_Text Label(Transform parent,string value,float top,float height,float size=20f)
    {
        GameObject go=new(value,typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);RectTransform r=go.GetComponent<RectTransform>();r.anchorMin=new Vector2(0f,1f);r.anchorMax=Vector2.one;r.pivot=new Vector2(0.5f,1f);r.anchoredPosition=new Vector2(0f,-top);r.sizeDelta=new Vector2(-28f,height);
        TMP_Text label=go.GetComponent<TMP_Text>();label.font=TMP_Settings.defaultFontAsset;label.fontSize=size;label.color=Color.white;label.text=value;label.raycastTarget=false;return label;
    }
    private Button Action(Transform parent,string title,float top,UnityEngine.Events.UnityAction handler)
    {
        GameObject go=new(title,typeof(RectTransform),typeof(Image),typeof(Button));go.transform.SetParent(parent,false);RectTransform r=go.GetComponent<RectTransform>();r.anchorMin=new Vector2(0f,1f);r.anchorMax=Vector2.one;r.pivot=new Vector2(0.5f,1f);r.anchoredPosition=new Vector2(0f,-top);r.sizeDelta=new Vector2(-24f,34f);go.GetComponent<Image>().color=accent;
        Button b=go.GetComponent<Button>();b.navigation=new Navigation{mode=Navigation.Mode.None};b.onClick.AddListener(handler);TMP_Text t=Label(go.transform,title,3f,30f,17f);t.alignment=TextAlignmentOptions.Center;return b;
    }
    private void Awake()
    {
        RectTransform controls=Panel("Training Controls",new Vector2(0f,1f),new Vector2(16f,-16f),new Vector2(300f,650f));
        Label(controls,"TRAINING ARENA",12f,32f,25f);nameText=Label(controls,"Loading catalog...",47f,35f,19f);
        Action(controls,"< PREVIOUS ENEMY",86f,manager.PreviousEnemy);Action(controls,"NEXT ENEMY >",124f,manager.NextEnemy);
        Action(controls,"COMBAT",170f,()=>manager.SetMode(TrainingManager.EnemyMode.Combat));Action(controls,"DUMMY",208f,()=>manager.SetMode(TrainingManager.EnemyMode.Dummy));Action(controls,"RESPAWN (2 sec)",246f,()=>manager.SetMode(TrainingManager.EnemyMode.Respawn));
        ai=Action(controls,"PAUSE AI",286f,manager.ToggleAI);
        resources=Action(controls,"INFINITE MP / VIGOR",326f,()=>manager.SetInfiniteResources(!manager.InfiniteResources));
        cooldown=Action(controls,"NO SKILL COOLDOWN",364f,()=>manager.SetNoCooldown(!manager.NoCooldown));
        god=Action(controls,"NO HP LOSS",402f,()=>manager.SetInvulnerable(!manager.Invulnerable));
        dummy=Action(controls,"DUMMY MIN HP = 1",440f,()=>manager.SetInfiniteDummy(!manager.InfiniteDummy));
        boxes=Action(controls,"HITBOXES",478f,()=>hitboxes.Visible=!hitboxes.Visible);
        Action(controls,"RESET STATISTICS",516f,manager.Statistics.Reset);Action(controls,"RESTART TARGET",554f,manager.RestartTarget);Action(controls,"RETURN TO MAP",600f,manager.ExitTraining);
        RectTransform stats=Panel("Damage Statistics",Vector2.one,new Vector2(-16f,-16f),new Vector2(335f,345f));Label(stats,"DAMAGE STATISTICS",12f,35f,23f);statsText=Label(stats,"Waiting for target...",53f,280f,20f);
        RectTransform player=Panel("Player Controls",new Vector2(0.5f,0f),new Vector2(0f,12f),new Vector2(680f,104f));playerText=Label(player,"Q/E switch | U/I/O skills",9f,90f,18f);
    }
    private static void State(Button button,string name,bool on){button.GetComponentInChildren<TMP_Text>().SetText(name+(on?": ON":": OFF"));button.GetComponent<Image>().color=on?accent:new Color(0.24f,0.29f,0.34f);}
    private void Update()
    {
        if(!manager.Ready || Time.unscaledTime<refreshAt)return;refreshAt=Time.unscaledTime+0.1f;
        nameText.SetText((manager.Selected+1)+" / "+manager.Catalog.Count+"  "+manager.Catalog.enemies[manager.Selected].displayName);
        State(resources,"INFINITE MP / VIGOR",manager.InfiniteResources);State(cooldown,"NO SKILL COOLDOWN",manager.NoCooldown);State(god,"NO HP LOSS",manager.Invulnerable);State(dummy,"DUMMY MIN HP = 1",manager.InfiniteDummy);State(boxes,"HITBOXES",hitboxes.Visible);State(ai,"AI PAUSED",manager.Mode==TrainingManager.EnemyMode.Dummy);
        TrainingStatistics s=manager.Statistics;text.Clear();text.Append("Mode: ").Append(manager.Mode).Append('\n').Append("DPS (last 3s): ").Append(s.Dps.ToString("0.0")).Append('\n').Append("Total damage: ").Append(s.TotalDamage.ToString("0.0")).Append('\n').Append("Hits: ").Append(s.Hits).Append('\n').Append("Critical hits: ").Append(s.CriticalHits).Append('\n').Append("Critical rate: ").Append((s.CriticalRate*100f).ToString("0.0")).Append("%\n").Append("Max hit: ").Append(s.MaximumHit.ToString("0.0")).Append('\n').Append("Time to kill: ").Append(s.KillTime<0f?"--":s.KillTime.ToString("0.00")+"s").Append('\n');
        if(manager.TargetHealth!=null)text.Append("Target HP: ").Append(manager.TargetHealth.CurBlood.ToString("0.0")).Append(" / ").Append(manager.TargetHealth.BeginBlood.ToString("0.0"));statsText.SetText(text);
        PlayerController p=manager.Players.CurrentPlayer;if(p==null)return;PlayerResources r=p.GetComponent<PlayerResources>();PlayerCombat c=p.GetComponent<PlayerCombat>();text.Clear();
        text.Append(r.Id).Append("   HP ").Append(p.GetComponent<Health>().CurBlood.ToString("0")).Append("   MP ").Append(r.CurrentMana.ToString("0")).Append("   VIGOR ").Append(r.CurrentVigor.ToString("0")).Append('\n').Append("Q/E switch   |   U/I/O skills   |   Normal game controls\n");
        for(int k=1;k<=c.SkillCount;k++)text.Append("Skill ").Append(k).Append(": ").Append(c.GetSkillCooldownRemaining(k).ToString("0.0")).Append("s    ");playerText.SetText(text);
    }
}
