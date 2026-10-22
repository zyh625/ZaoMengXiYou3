using System;
using Unity.VisualScripting;
using UnityEngine;

public class EnemyMovement : MonoBehaviour
{
    protected Rigidbody2D body;
    [SerializeField] private float moveSpeed, backSpeed, upSpeed, slowSpeed;//各种速度
    [SerializeField] private float minNormalY;
    private ContactFilter2D groundFilter;
    private ContactPoint2D[] contacts = new ContactPoint2D[16];
    private bool isGround;
    public bool IsGround => isGround;
    public bool LandedThisFrame = false;
    private Transform player;
    public float DeltaX => player.position.x - transform.position.x;

    void Start()
    {
        body = GetComponent<Rigidbody2D>();
    }
    public void Move(bool slow)
    {
        Vector2 velocity = body.linearVelocity;
        velocity.x = (slow ? slowSpeed : moveSpeed) * (DeltaX > 0f ? 1f : -1f);
        body.linearVelocity= velocity;
    }
    public void AttackedMove(bool r,bool up)
    {
        Vector2 velocity = body.linearVelocity;
        velocity.x = backSpeed * (r ? 1f : -1f);
        velocity.y += up ? upSpeed : 0f;
        body.linearVelocity = velocity;
    }
    public void Stop()
    {
        body.linearVelocity = new Vector2(0f, body.linearVelocityY);
    }
    public void CheckIsGround()
    {
        bool wasGround = isGround;
        isGround = CheckGround();
        LandedThisFrame = isGround && !wasGround;
    }
    private bool CheckGround()
    {
        float y = body.linearVelocityY;
        if (Mathf.Abs(y) > 0.1f)
            return false;
        int count = body.GetContacts(groundFilter, contacts);
        for (int i = 0; i < count; i++)
        {
            if (contacts[i].normal.y >= minNormalY)
                return true; //接触面的法向量超出最小y向量
        }
        return false;
    }
    public int AirState()//-1下落，0在地面，1上升
    {
        if (isGround) return 0;
        return body.linearVelocityY > 0.1f ? 1 : -1;
    }
    private void OnDisable()
    {
        if (body != null) body.linearVelocity = Vector2.zero;
        isGround = true;
    }
    public void SetTarget(Transform tran) => player = tran;
}
