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
    public event Action SkillOnePressed, SkillTwoPressed, SkillThreePressed;
    public event Action SwitchPriPressed;//切换上一个角色
    public event Action SwitchNextPressed;//切换下一个角色
    public event Action InteractPressed;//W，从出口出去
    public event Action QuickDownPressed;//S,从空中快速下降
    private InputActionAsset curInputAction;//输入系统实例
    private InputActionMap playerMap;
    private InputAction moveButton, jumpButton;
    private InputAction attackButton, boostButton;
    private InputAction skillOneButton, skillTwoButton, skillThreeButton;
    private InputAction switchPriButton, switchNextButton;
    private InputAction interactButton, quickDownButton;



    private void Awake()
    {
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
    }
    private void OnDisable()
    {
        playerMap?.Disable();
        moveButton.performed -= OnMove;
        moveButton.canceled -= OnMove;
        jumpButton.performed -= OnJump;
    }

    private void OnMove(InputAction.CallbackContext ctx) => MovePressed?.Invoke(ctx);
    private void OnJump(InputAction.CallbackContext ctx) => JumpPressed?.Invoke();
    private void Update()
    {
        if (attackButton.WasPressedThisFrame())
            AttackPressed.Invoke();
        if (boostButton.WasPressedThisFrame())
            BoostPressed.Invoke();
        if (skillOneButton.WasPressedThisFrame())
            SkillOnePressed.Invoke();
        if (skillTwoButton.WasPressedThisFrame())
            SkillTwoPressed.Invoke();
        if (skillThreeButton.WasPressedThisFrame())
            SkillThreePressed.Invoke();
        if (switchPriButton.WasPressedThisFrame())
            SwitchPriPressed.Invoke();
        if (switchNextButton.WasPressedThisFrame())
            SwitchNextPressed.Invoke();
        if (interactButton.WasPressedThisFrame())
            InteractPressed.Invoke();
        if (quickDownButton.WasPressedThisFrame())
            QuickDownPressed.Invoke();
    }
}
