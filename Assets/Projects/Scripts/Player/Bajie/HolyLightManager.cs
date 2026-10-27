using UnityEngine;

public class HolyLightManager : MonoBehaviour
{
    [SerializeField] private SpriteData light;//自身圣光的完整十六帧动画
    [SerializeField] private SpriteManager SM;//独立子Visual的动画组件
    private SpriteRenderer lightRenderer;//圣光渲染器，不修改人物的动画或材质
    private bool playing;//记录本次圣光是否仍在播放

    private void OnEnable()
    {
        InitializeVisual();
        playing = false;
        SetVisualActive(false);
    }

    public void OpenLight()//立即从第一帧播放整段圣光，重复释放时重新开始
    {
        InitializeVisual();
        if (SM == null || light == null || light.sprites == null || light.sprites.Length == 0)
            return;

        UpdateSortingOrder();
        SetVisualActive(true);
        SM.ChangeState(new SpriteData
        {
            sprites = light.sprites,
            fps = light.fps,
            loop = false
        });
        playing = true;
    }

    private void Update()
    {
        if (!playing || SM == null || !SM.IsFinished) return;
        playing = false;
        SetVisualActive(false);
    }

    private void InitializeVisual()//包括尚未激活的子Visual，支持首次启用前完成接线
    {
        if (SM == null) SM = GetComponentInChildren<SpriteManager>(true);
        if (SM != null && lightRenderer == null)
            lightRenderer = SM.GetComponent<SpriteRenderer>();
    }

    private void SetVisualActive(bool active)//不禁用承载管理器的对象，保证能检测动画结束
    {
        if (SM == null) return;
        if (SM.gameObject != gameObject) SM.gameObject.SetActive(active);
        if (lightRenderer != null) lightRenderer.enabled = active;
    }

    private void UpdateSortingOrder()//按人物当前层级显示，覆盖关卡额外增加的显示顺序
    {
        PlayerController player = GetComponentInParent<PlayerController>(true);
        if (player == null || lightRenderer == null) return;
        Transform bodyVisual = player.transform.Find("Visual");
        SpriteRenderer bodyRenderer = bodyVisual == null ? null : bodyVisual.GetComponent<SpriteRenderer>();
        if (bodyRenderer == null) return;
        lightRenderer.sortingLayerID = bodyRenderer.sortingLayerID;
        lightRenderer.sortingOrder = bodyRenderer.sortingOrder + 1;
    }
}
