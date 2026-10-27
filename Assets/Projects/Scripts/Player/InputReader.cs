using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputReader : MonoBehaviour
{
    [SerializeField] private InputActionAsset inputAction;

    private float lastInteract = -1f;//上次按下W键的时间,用于判断组合技W+J
    public float LastInteract => lastInteract;

    public event Action<InputAction.CallbackContext> MovePressed;
    public event Action JumpPressed, AttackPressed, BoostPressed;
    public event Action <int>SkillOnePressed, SkillTwoPressed, SkillThreePressed;
    public event Action SwitchPriPressed;//切换上一个角色
    public event Action SwitchNextPressed;//切换下一个角色
    public event Action InteractPressed;//W，从出口出去
    public event Action QuickDownPressed;//S,从空中快速下降
    public event Action QuickDownReleased;//S松开，供需要持续按住的组合技结束使用
    private InputActionAsset curInputAction;//输入系统实例
    private InputActionMap playerMap;
    private InputAction moveButton, jumpButton;
    private InputAction attackButton, boostButton;
    private InputAction skillOneButton, skillTwoButton, skillThreeButton;
    private InputAction switchPriButton, switchNextButton;
    private InputAction interactButton, quickDownButton;
    private ActorStatus status;//冻结时舍弃操作事件，不在恢复后补放之前按过的攻击和技能
    public float MoveValue => moveButton != null ? moveButton.ReadValue<float>() : 0f;//解冻后读取实际仍按住的移动轴，不重新触发一次按下事件
    private bool ActionsBlocked => status != null && status.BlocksActions;
    private bool MovementBlocked => status != null && status.BlocksMovement;
    public bool AttackHeld => attackButton != null
        && attackButton.IsPressed();
    public bool QuickDownHeld => quickDownButton != null && quickDownButton.IsPressed();//读取Action，保持支持改键



    private void Awake()
    {
        status = GetComponent<Health>()?.Status;
        if (inputAction == null)
        {
            Debug.Log("无输入系统"); return;
        }
        curInputAction = Instantiate(inputAction);
        playerMap = curInputAction.FindActionMap("Player", true);//找不到就抛异常
        moveButton = playerMap.FindAction("Move", true);
        jumpButton = playerMap.FindAction("Jump", true);
        attackButton = playerMap.FindAction("Attack", true);
        boostButton = playerMap.FindAction("Boost", true);
        skillOneButton = playerMap.FindAction("SkillOne", true);
        skillTwoButton = playerMap.FindAction("SkillTwo", true);
        skillThreeButton = playerMap.FindAction("SkillThree", true);
        switchPriButton = playerMap.FindAction("SwitchPri", true);
        switchNextButton = playerMap.FindAction("SwitchNext", true);
        interactButton = playerMap.FindAction("Interact", true);
        quickDownButton = playerMap.FindAction("QuickDown", true);
    }
    private void OnEnable()
    {
        playerMap?.Enable();
        moveButton.performed += OnMove;
        moveButton.canceled += OnMove;
        jumpButton.performed += OnJump;
        attackButton.performed += OnAttack;
        skillOneButton.performed += OnSkillOne;
        skillTwoButton.performed += OnSkillTwo;
        skillThreeButton.performed += OnSkillThree;
        quickDownButton.performed += OnQuickDown;
        quickDownButton.canceled += OnQuickDownReleased;
    }
    private void OnDisable()
    {
        playerMap?.Disable();
        moveButton.performed -= OnMove;
        moveButton.canceled -= OnMove;
        jumpButton.performed -= OnJump;
        attackButton.performed-= OnAttack;
        skillOneButton.performed -= OnSkillOne;
        skillTwoButton.performed -= OnSkillTwo;
        skillThreeButton.performed -= OnSkillThree;
        quickDownButton.performed -= OnQuickDown;
        quickDownButton.canceled -= OnQuickDownReleased;
    }

    private void OnMove(InputAction.CallbackContext ctx)
    {
        if (!ActionsBlocked) MovePressed?.Invoke(ctx);
    }
    private void OnJump(InputAction.CallbackContext ctx)
    {
        if (!MovementBlocked) JumpPressed?.Invoke();
    }
    private void OnAttack(InputAction.CallbackContext ctx)
    {
        if (!ActionsBlocked) AttackPressed?.Invoke();
    }
    private void OnSkillOne(InputAction.CallbackContext ctx)
    {
        if (!ActionsBlocked) SkillOnePressed?.Invoke(1);
    }
    private void OnSkillTwo(InputAction.CallbackContext ctx)
    {
        if (!ActionsBlocked) SkillTwoPressed?.Invoke(2);
    }
    private void OnSkillThree(InputAction.CallbackContext ctx)
    {
        if (!ActionsBlocked) SkillThreePressed?.Invoke(3);
    }
    private void OnQuickDown(InputAction.CallbackContext ctx)
    {
        if (!MovementBlocked) QuickDownPressed?.Invoke();
    }
    private void OnQuickDownReleased(InputAction.CallbackContext ctx) => QuickDownReleased?.Invoke();

    private void Update()
    {
        if (ActionsBlocked) return;
        if (boostButton.WasPressedThisFrame())
            BoostPressed?.Invoke();
        if (switchPriButton.WasPressedThisFrame())
        {
            SwitchPriPressed?.Invoke();
            return;//换人可能已停用当前对象，不再继续处理旧角色本帧的输入
        }
        if (switchNextButton.WasPressedThisFrame())
        {
            SwitchNextPressed?.Invoke();
            return;
        }
        if (interactButton.WasPressedThisFrame())
            InteractPressed?.Invoke();
    }
}
