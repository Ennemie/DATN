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
    private bool isCrouching = false;
    private float speed = 5f;
    [HideInInspector] public bool acceptInput;
    private GameObject soundWaveEffect;

    // Attack
    [HideInInspector] public bool isAttacking = false;
    private bool isReadyToAttack = true;

    // Interact
    private InteractController interactProp = null;
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        acceptInput = true;
        soundWaveEffect = transform.Find("SoundWaveEffect").gameObject;
        soundWaveEffect.SetActive(false);
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
            interactProp.Interact();

            if (interactProp.interactType == InteractController.InteractType.Commander)
            {
                PlayerCanvasController.Instance.ChangeToFist();
                LookAtNPC(interactProp);
                PlayerState.Instance.CurrentState = PlayerState.State.Talking;
            }
        }
    }
    public void OnNextLineConversation(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            if (interactProp == null) return;
            interactProp.NextLineConversation();
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
    public void OnCrouchInput(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            ToggleCrouch();
        }
    }
    public void ToggleCrouch()
    {
        isCrouching = !isCrouching;
        speed = isCrouching ? 2.5f : 5f;
        PlayerCanvasController.Instance.UpdateCrouchIcon(isCrouching);
    }
    void FixedUpdate()
    {
        moveDirection = new Vector3(inputVector.x, 0, inputVector.y);

        // 1. TRƯỜNG HỢP ĐANG TẤN CÔNG
        if (isAttacking)
        {
            PlayerState.Instance.CurrentState = PlayerState.State.Attack;

            // Khi tấn công thì khóa chặt X, Z để không bị đẩy lùi hoặc trượt đi
            rb.constraints |= RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ;

            if (PlayerWeapon.Instance.CurrentWeapon == PlayerWeapon.WeaponType.Pistol)
            {
                PistolShooting.Instance.Shoot();
            }
            
            soundWaveEffect.SetActive(true);

            return; // Thoát hàm, không chạy logic di chuyển phía dưới
        }

        // 2. TRƯỜNG HỢP CÓ BẤM DI CHUYỂN
        if (moveDirection.sqrMagnitude > 0.01f)
        {
            // Mở khóa X, Z để chuẩn bị di chuyển
            rb.constraints &= ~(RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ);

            if(isCrouching)
            {
                PlayerState.Instance.CurrentState = PlayerState.State.Crouching;
            }
            else
            {
                PlayerState.Instance.CurrentState = PlayerState.State.Running;
                soundWaveEffect.SetActive(true);
            }
            moveDirection.Normalize();

            // Thực hiện di chuyển và xoay nhân vật
            rb.MovePosition(rb.position + moveDirection * speed * Time.fixedDeltaTime);
            rb.rotation = Quaternion.LookRotation(moveDirection);
        }
        // 3. TRƯỜNG HỢP ĐỨNG YÊN (KHÔNG DI CHUYỂN)
        else
        {
            if (PlayerState.Instance.CurrentState != PlayerState.State.Talking)
            {
                PlayerState.Instance.CurrentState = PlayerState.State.Idle;
            }
            soundWaveEffect.SetActive(false);

            // Khi đứng yên: Khóa cứng X, Z để tránh bị các ngoại lực vật lý (va chạm, quái đẩy) làm dịch chuyển
            rb.constraints |= RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Interactive Obj"))
        {
            if (interactProp != null) interactProp.HideMessage();
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
