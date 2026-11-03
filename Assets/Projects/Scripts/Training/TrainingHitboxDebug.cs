using System.Collections.Generic;
using UnityEngine;

public sealed class TrainingHitboxDebug : MonoBehaviour
{
    private sealed class BoxView {public BoxCollider2D box;public LineRenderer line;}
    private readonly List<BoxView> views=new(128);
    private readonly HashSet<BoxCollider2D> registered=new();
    private Material material;
    private float scanAt;
    public bool Visible {get;set;}
    public int RegisteredCount=>views.Count;
    private void Awake(){material=new Material(Shader.Find("Sprites/Default"));}
    private void LateUpdate()
    {
        if(!TrainingSession.Active)return;
        if(Visible && Time.unscaledTime>=scanAt)
        {
            scanAt=Time.unscaledTime+0.5f;//只在调试开启时低频登记新池化判定；逐帧绘制不产生数组/集合。
            foreach(BoxCollider2D box in FindObjectsByType<BoxCollider2D>(FindObjectsInactive.Include))
            {
                if(box.gameObject.scene!=gameObject.scene || registered.Contains(box) || box.GetComponent<AttackBox>()==null && box.GetComponent<EnemyCheckAttack>()==null)continue;
                registered.Add(box);GameObject go=new("Debug_"+box.name);go.transform.SetParent(transform,false);LineRenderer line=go.AddComponent<LineRenderer>();line.sharedMaterial=material;line.positionCount=5;line.startWidth=line.endWidth=0.035f;line.useWorldSpace=true;line.sortingOrder=1000;
                line.startColor=line.endColor=box.GetComponent<AttackBox>()!=null?new Color(1f,0.15f,0.1f):new Color(0.1f,0.9f,1f);views.Add(new BoxView{box=box,line=line});
            }
        }
        for(int i=views.Count-1;i>=0;i--)
        {
            BoxView view=views[i];if(view.box==null){registered.Remove(view.box);Destroy(view.line.gameObject);views.RemoveAt(i);continue;}
            view.line.enabled=Visible && view.box.enabled && view.box.gameObject.activeInHierarchy;if(!view.line.enabled)continue;
            Vector2 o=view.box.offset,h=view.box.size/2f;Transform t=view.box.transform;
            view.line.SetPosition(0,t.TransformPoint(o+new Vector2(-h.x,-h.y)));view.line.SetPosition(1,t.TransformPoint(o+new Vector2(h.x,-h.y)));
            view.line.SetPosition(2,t.TransformPoint(o+new Vector2(h.x,h.y)));view.line.SetPosition(3,t.TransformPoint(o+new Vector2(-h.x,h.y)));view.line.SetPosition(4,t.TransformPoint(o+new Vector2(-h.x,-h.y)));
        }
    }
    private void OnDestroy(){if(material!=null)Destroy(material);}
}
