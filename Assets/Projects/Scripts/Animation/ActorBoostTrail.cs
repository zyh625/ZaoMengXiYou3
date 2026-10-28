using System.Collections.Generic;
using UnityEngine;

//玩家和敌人共用的无双残影，保留原组件名称和开启入口，避免已有预制体引用失效。
[DefaultExecutionOrder(10020)]
public class ActorBoostTrail : MonoBehaviour
{
    [SerializeField] private SpriteRenderer bodyRenderer;//留空时优先使用玩家本体、Visual或根节点的SpriteRenderer
    [SerializeField] private Material trailMaterial;//留空时自动加载Resources/Effects/BlackBoostAfterimage，不使用人物描边材质
    [SerializeField] private Color trailColor = new(0f, 0f, 0f, 0.16f);//默认淡黑色，透明度以60帧每秒为参照，高帧率时自动减淡
    [SerializeField, Min(0.01f)] private float afterimageDuration = 0.2f;//每张残影从出现到完全透明的秒数
    [SerializeField, Min(0.001f)] private float minimumMoveSpeed = 0.03f;//过滤地面接触产生的微小位置抖动
    [SerializeField, Min(0)] private int prewarmCount = 16;//首次开启时准备的残影数量，高帧率下会自动扩容并复用
    [SerializeField] private int sortingOffset = -1;//残影排在本体后面

    private sealed class Afterimage//每个对象只创建一次，后续生成残影只更新精灵和计时
    {
        public SpriteRenderer renderer;
        public float age, duration, initialAlpha;//每张残影独立记录淡出过程
    }

    private readonly List<Afterimage> afterimages = new();//只保存本组件拥有的对象，停用和销毁时统一清理
    private readonly Stack<Afterimage> available = new();//已经淡出的残影直接归还，避免逐帧创建和销毁对象
    private GameObject visual;//残影容器留在世界坐标中，不随角色移动、转向或缩放
    private Rigidbody2D body;//物理位置采样供没有新物理帧的渲染帧继续使用
    private Health health;//死亡时立即清理，不依赖角色是否已经停用
    private Vector3 previousPosition;//根节点的位置不会因为切换动画精灵而变化
    private Vector2 previousPhysicsPosition;//记录上一次已经完成的物理步位置
    private float previousPhysicsTime;//多个渲染帧之间没有物理步时不把移动误判为停止
    private bool active, physicsMoving;//active只控制新残影的生成，已经生成的残影仍完成淡出

    private void Awake() => ResolveBody();

    private void ResolveBody()//只缓存本体引用，不把护盾、水柱或其他子特效当成人物
    {
        if (body == null) body = GetComponent<Rigidbody2D>();
        if (health == null) health = GetComponent<Health>();
        if (bodyRenderer != null) return;
        PlayerController player = GetComponent<PlayerController>();
        if (player != null && player.BodySpriteManager != null)
            bodyRenderer = player.BodySpriteManager.GetComponent<SpriteRenderer>();
        if (bodyRenderer == null) bodyRenderer = transform.Find("Visual")?.GetComponent<SpriteRenderer>();
        if (bodyRenderer == null) bodyRenderer = GetComponent<SpriteRenderer>();
        if (bodyRenderer == null) bodyRenderer = GetComponentInChildren<SpriteRenderer>(true);
    }

    private void OnEnable()
    {
        ResolveBody();
        if (health != null) health.Died += OnDied;
    }

    public void SetActive(bool enabled)//无双和未来敌人强化状态沿用同一个开启、关闭入口
    {
        if (!enabled)
        {
            active = physicsMoving = false;
            return;//正常结束无双后，最后留下的残影仍会在自己的持续时间内淡出
        }
        if (active) return;
        ResolveBody();
        if (!isActiveAndEnabled || bodyRenderer == null || health != null && health.Dead || !CreateVisual()) return;
        active = true;
        physicsMoving = false;
        previousPosition = transform.position;
        previousPhysicsPosition = body != null ? body.position : (Vector2)previousPosition;
        previousPhysicsTime = Time.fixedTime;
    }

    private bool CreateVisual()//所有残影共用一个无光照材质，不复制本体的描边或受击效果
    {
        if (visual != null) return true;
        if (trailMaterial == null) trailMaterial = Resources.Load<Material>("Effects/BlackBoostAfterimage");
        if (trailMaterial == null)
        {
            Debug.LogError("ActorBoostTrail缺少Resources/Effects/BlackBoostAfterimage材质", this);
            return false;
        }
        visual = new GameObject($"{name} Boost Afterimages");
        for (int i = 0; i < Mathf.Max(0, prewarmCount); i++)
            available.Push(CreateAfterimage());
        return true;
    }

    private Afterimage CreateAfterimage()//超出预热数量时按需增加，淡出后继续复用
    {
        GameObject image = new($"Afterimage {afterimages.Count + 1}");
        image.layer = gameObject.layer;
        image.transform.SetParent(visual.transform, false);
        SpriteRenderer renderer = image.AddComponent<SpriteRenderer>();
        renderer.sharedMaterial = trailMaterial;
        renderer.enabled = false;
        Afterimage afterimage = new() { renderer = renderer };
        afterimages.Add(afterimage);
        return afterimage;
    }

    private void LateUpdate()//在人物切完动画和更新位置之后，每个渲染帧最多留下一张当时的精灵
    {
        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0f) return;//暂停时不生成残影，已有残影的淡出计时也一起暂停
        FadeAfterimages(deltaTime);
        if (!active) return;
        if (health != null && health.Dead || bodyRenderer == null)
        {
            SetActive(false);
            ClearAfterimages();
            return;
        }
        if (!IsMoving(deltaTime) || !bodyRenderer.enabled || !bodyRenderer.gameObject.activeInHierarchy || bodyRenderer.sprite == null)
            return;
        LeaveAfterimage(deltaTime);
    }

    private bool IsMoving(float deltaTime)//依据实际位移而非按键，顶住墙壁不留下残影，空中或技能位移也可以留下
    {
        Vector3 position = transform.position;
        float minimumSpeedSquared = minimumMoveSpeed * minimumMoveSpeed;
        bool frameMoving = (position - previousPosition).sqrMagnitude > minimumSpeedSquared * deltaTime * deltaTime;
        previousPosition = position;
        if (body == null) return frameMoving;

        Vector2 physicsPosition = body.position;
        float physicsTime = Time.fixedTime;
        float elapsed = physicsTime - previousPhysicsTime;
        if (elapsed > 0f)
        {
            physicsMoving = (physicsPosition - previousPhysicsPosition).sqrMagnitude > minimumSpeedSquared * elapsed * elapsed;
            previousPhysicsPosition = physicsPosition;
            previousPhysicsTime = physicsTime;
        }
        else if (physicsPosition != previousPhysicsPosition)//兼容其他脚本直接设置刚体位置的位移
        {
            physicsMoving = (physicsPosition - previousPhysicsPosition).sqrMagnitude > minimumSpeedSquared * deltaTime * deltaTime;
            previousPhysicsPosition = physicsPosition;
        }
        return frameMoving || physicsMoving;
    }

    private void LeaveAfterimage(float deltaTime)//复制的是此刻的精灵和变换，之后不会再跟随本体切帧或翻转
    {
        Afterimage afterimage = available.Count > 0 ? available.Pop() : CreateAfterimage();
        SpriteRenderer renderer = afterimage.renderer;
        Transform source = bodyRenderer.transform;
        renderer.transform.SetPositionAndRotation(source.position, source.rotation);
        renderer.transform.localScale = source.lossyScale;
        renderer.sprite = bodyRenderer.sprite;
        renderer.flipX = bodyRenderer.flipX;
        renderer.flipY = bodyRenderer.flipY;
        renderer.drawMode = bodyRenderer.drawMode;
        renderer.size = bodyRenderer.size;
        renderer.tileMode = bodyRenderer.tileMode;
        renderer.spriteSortPoint = bodyRenderer.spriteSortPoint;
        renderer.maskInteraction = bodyRenderer.maskInteraction;
        renderer.sortingLayerID = bodyRenderer.sortingLayerID;
        renderer.sortingOrder = bodyRenderer.sortingOrder + sortingOffset;
        afterimage.age = 0f;
        afterimage.duration = Mathf.Max(0.01f, afterimageDuration);
        float referenceAlpha = Mathf.Clamp01(trailColor.a * bodyRenderer.color.a);//按60帧参照归一化，避免高帧率生成更多残影后叠加得过黑
        float frameWeight = Mathf.Min(1f, deltaTime * 60f);//低帧率不把单张残影加深到超过配置透明度
        afterimage.initialAlpha = 1f - Mathf.Pow(1f - referenceAlpha, frameWeight);
        Color color = trailColor;
        color.a = afterimage.initialAlpha;
        renderer.color = color;
        renderer.enabled = true;
    }

    private void FadeAfterimages(float deltaTime)//只改变颜色透明度，保留残影生成时的姿态、大小和世界位置
    {
        foreach (Afterimage afterimage in afterimages)
        {
            SpriteRenderer renderer = afterimage.renderer;
            if (!renderer.enabled) continue;
            afterimage.age += deltaTime;
            if (afterimage.age >= afterimage.duration)
            {
                renderer.enabled = false;
                renderer.sprite = null;
                available.Push(afterimage);
                continue;
            }
            Color color = renderer.color;
            color.a = afterimage.initialAlpha * (1f - afterimage.age / afterimage.duration);
            renderer.color = color;
        }
    }

    private void ClearAfterimages()//换人、死亡或对象池回收时不能留下旧角色的残影
    {
        available.Clear();
        foreach (Afterimage afterimage in afterimages)
        {
            afterimage.renderer.enabled = false;
            afterimage.renderer.sprite = null;
            available.Push(afterimage);
        }
    }

    private void OnDied(DamageInfo damage)
    {
        SetActive(false);
        ClearAfterimages();
    }

    private void OnDisable()
    {
        if (health != null) health.Died -= OnDied;
        SetActive(false);
        ClearAfterimages();
    }

    private void OnDestroy()
    {
        if (visual != null) Destroy(visual);
    }
}
