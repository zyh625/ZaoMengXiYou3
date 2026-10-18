using UnityEngine;

public class PlayerInteract : MonoBehaviour
{
    [SerializeField] private float InteractMemory = 0.6f;
    private float lastInteractTime = -1f;
    public void OnInteract()
    {
        lastInteractTime = Time.time;
    }
    public bool IsInteracting()
    {
        return Time.time - lastInteractTime <= InteractMemory;
    }
}
