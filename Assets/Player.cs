using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
public class Player : MonoBehaviour
{
    public GameManager GameManager;
    float lastPressTime = -1;
    float doublePressTime = 0.3f;
    Vector2 input;//检测是否按下A、D键
    Vector2 priInput;//用于判断上次按下的按键是否和这次一样
    float speed = 0.003f;
    int jumped = 0;//检测跳跃次数
    void Update()
    {
        if (input.x != 0)
        {
            GameManager.Move(input.x > 0, speed);
        }
    }
    public void OnMove(InputAction.CallbackContext ctx)
    {
        input = ctx.ReadValue<Vector2>();
        if (ctx.started)
        {
            InitMonkey(1);
            if (lastPressTime!=-1&&Time.time - lastPressTime <= doublePressTime&&priInput.x==input.x)
            {
                InitMonkey(2);
                speed = 0.005f;
            }
            lastPressTime = Time.time;
            priInput = input;
        }
        if (ctx.canceled)
        {
            InitMonkey(0);
            speed = 0.003f;
        }
    }
    public void OnJump(InputAction.CallbackContext ctx)
    {
        if (GameManager.rb.linearVelocity.y ==0)
        {
            jumped = 0;
        }
        if (ctx.performed)
        {
            if (jumped == 2) return;
            GameManager.Jump();
            InitMonkey(3 + jumped);
            jumped++;
        }
    }
    public void InitMonkey(int k)
    {
        GameManager.act = k;
        GameManager.curFrame = 0;
    }
}
