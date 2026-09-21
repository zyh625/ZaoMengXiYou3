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
    float speed = 0.1f;
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
            GameManager.act = 1;
            if (lastPressTime!=-1&&Time.time - lastPressTime <= doublePressTime&&priInput.x==input.x)
            {
                GameManager.act = 2;
                speed = 0.2f;
            }
            lastPressTime = Time.time;
            priInput = input;
        }
        if (ctx.canceled)
        {
            GameManager.act = 0;
            speed = 0.1f;
        }
    }
    public void OnJump(InputAction.CallbackContext ctx)
    {
        if (ctx.performed)
        {
            GameManager.Jump();
        }
    }
}
