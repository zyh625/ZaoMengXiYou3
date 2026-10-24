using UnityEngine;

public class ExitManager : MonoBehaviour
{
    [SerializeField] private SpriteData sprites;
    private SpriteManager SM;
    private bool playerIn = false;
    public bool PlayerIn => playerIn;
    private BoxCollider2D box;
    private void Awake() => SM = GetComponentInChildren<SpriteManager>();
    private void OnEnable() => SM.ChangeState(sprites);
    private void OnTriggerEnter2D(Collider2D other) => playerIn = true;
    private void OnTriggerStay2D(Collider2D other) => playerIn = true;
    private void OnTriggerExit2D(Collider2D collision) => playerIn = false;
}
