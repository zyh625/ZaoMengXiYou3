using UnityEngine;

public class PlayerInteract : MonoBehaviour
{
    [SerializeField] private float InteractMemory = 0.6f;
    private float lastInteractTime = -1f;
    public void OnInteract()
    {
        lastInteractTime = Time.time;
    }
    //换人时丢弃旧角色的W键缓存，避免切回后自动交互或误触上挑。
    public void ClearRequest() => lastInteractTime = -1f;
    public bool IsInteracting()
    {
        return Time.time - lastInteractTime <= InteractMemory;
    }
}
