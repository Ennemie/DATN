using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using DG.Tweening;

public class PlayerController : MonoBehaviour
{
    public static PlayerController Instance { get; private set; }

    // Movement
    private Rigidbody rb;
    private Vector2 inputVector;
    private Vector3 moveDirection;
    [SerializeField] private float speed = 5f;
    [HideInInspector] public bool acceptInput;

    // Attack
    [HideInInspector] public bool isAttacking = false;
    private bool isReadyToAttack = true;

    // Interact
    private InteractController interactProp = null;
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        acceptInput = true;
    }
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    public void OnMoveInput(InputAction.CallbackContext context)
    {
        if (acceptInput)
        {
            inputVector = context.ReadValue<Vector2>();
        }
    }
    public void OnAttackInput(InputAction.CallbackContext context)
    {
        if(EventSystem.current.IsPointerOverGameObject()) return;
        if (context.performed && isReadyToAttack && acceptInput)
        {
                isAttacking = true;
                isReadyToAttack = false;
                StartCoroutine(AttackCoolDown());
        }
    }
    private IEnumerator AttackCoolDown()
    {
        yield return new WaitForSeconds(1f);
        isReadyToAttack = true;
    }

    public void OnInteractInput(InputAction.CallbackContext context)
    {
        if (context.started && interactProp != null && acceptInput)
        {
            if (interactProp.interactType == InteractController.InteractType.Door) interactProp.DoorInteract();
            if (interactProp.interactType == InteractController.InteractType.Commander)
            {
                PlayerCanvasController.Instance.ChangeToFist();
                interactProp.CommanderTalk();
                LookAtNPC(interactProp);
                PlayerState.Instance.CurrentState = PlayerState.State.Talking;
            }
        }
    }
    public void OnWeaponChangeInput(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            PlayerCanvasController.Instance.ShowWeaponsHubOnMouse(Mouse.current.position.ReadValue());
        }
        if(context.canceled)
        {
            PlayerCanvasController.Instance.RepositionWeaponsHub();
        }
    }
    public void OnInventorySwitch(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            PlayerCanvasController.Instance.ToggleInventory();
        }
    }
    void FixedUpdate()
    {
        moveDirection = new Vector3(inputVector.x, 0, inputVector.y);

        if (isAttacking)
        {
            PlayerState.Instance.CurrentState = PlayerState.State.Attack;
            if(PlayerWeapon.Instance.CurrentWeapon == PlayerWeapon.WeaponType.Pistol)
            {
                PistolShooting.Instance.Shoot();
            }
            return; // Không di chuyển khi đang tấn công
        }
        else if (moveDirection.sqrMagnitude > 0.01f)
        {
            PlayerState.Instance.CurrentState = PlayerState.State.Running;
            moveDirection.Normalize();
        }
        else if(PlayerState.Instance.CurrentState != PlayerState.State.Talking)
        {
            PlayerState.Instance.CurrentState = PlayerState.State.Idle;
        }

        rb.MovePosition(rb.position + moveDirection * speed * Time.fixedDeltaTime);

        if (moveDirection.sqrMagnitude > 0.01f)
        {
            rb.rotation = Quaternion.LookRotation(moveDirection);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Interactive Obj"))
        {
            other.TryGetComponent<InteractController>(out interactProp);
            if (interactProp != null) interactProp.ShowMessage();
        }
    }
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Interactive Obj"))
        {
            if (interactProp != null) interactProp.HideMessage();
            interactProp = null;
        }
    }
    private void LookAtNPC(InteractController npc)
    {
        Vector3 targetPos = npc.transform.position - transform.position;
        targetPos.y = 0; // Giữ nguyên trục Y để chỉ xoay quanh trục đứng
        if (targetPos.sqrMagnitude > 0.01f) // Tránh rung lắc khi quá gần
        {
            transform.DOLookAt(npc.transform.position, 0.5f).SetEase(Ease.OutQuad);
        }
    }
}
