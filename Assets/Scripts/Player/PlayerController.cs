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
    [HideInInspector] public bool isEnemyOnAttackRange = false;

    // Interact
    private InteractController interactProp = null;

    // Collider
    private CapsuleCollider playerCollider;
    private float originalColliderCenterY = 0.8482664f;
    private float originalColliderHeight = 1.882319f;
    private float crouchColliderCenterY = 0.4959992f;
    private float crouchColliderHeight = 1.177785f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        playerCollider = GetComponent<CapsuleCollider>();
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
            if(isCrouching) UpdateColliderForCrouch(true);
        }
        if(context.canceled)
        {
            if(isCrouching) UpdateColliderForCrouch(false);
        }
    }
    public void OnAttackInput(InputAction.CallbackContext context)
    {
        if(EventSystem.current.IsPointerOverGameObject())
            return;

        if (!context.performed ||
            !acceptInput)
        {
            return;
        }

        StealthTakedownController meleeController =
            GetComponent<StealthTakedownController>();

        if (meleeController != null &&
            meleeController.TryPunch())
        {
            return;
        }

        // The new performative Punch replaces the old generic fist punch.
        // Do not fall back to Fist_Punch when no paired target is valid.
        if (PlayerWeapon.Instance != null &&
            PlayerWeapon.Instance.CurrentWeapon ==
                PlayerWeapon.WeaponType.Fist)
        {
            return;
        }

        if (isReadyToAttack)
            Attack();
    }
    private void Attack()
    {
        isAttacking = true;
        isReadyToAttack = false;
        DOVirtual.DelayedCall(1f, () => {
            PlayerAttackRangeController.Instance.HitEnemy();
        });
        StartCoroutine(AttackCoolDown());
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
        // Chức năng mới: không mở Weapon Hub khi Player đang ở action/death state độc quyền.
        if (!acceptInput)
            return;

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
        // Chức năng mới: Inventory không được mở trong lúc Player bị khóa bởi interaction/death.
        if (!acceptInput)
            return;

        if (context.performed)
        {
            PlayerCanvasController.Instance.ToggleInventory();
        }
    }
    public void OnCrouchInput(InputAction.CallbackContext context)
    {
        // Chức năng mới: không cho đổi crouch trong lúc Player bị khóa bởi action/death.
        if (!acceptInput)
            return;

        if (context.performed)
        {
            ToggleCrouch();
        }
    }
    public void ToggleCrouch()
    {
        isCrouching = !isCrouching;
        if(!isCrouching) UpdateColliderForCrouch(false);
        speed = isCrouching ? 2.5f : 5f;
        PlayerCanvasController.Instance.UpdateCrouchIcon(isCrouching);
    }
    private void UpdateColliderForCrouch(bool isCrouching)
    {
        if (isCrouching)
        {
            playerCollider.center = new Vector3(playerCollider.center.x, crouchColliderCenterY, playerCollider.center.z);
            playerCollider.height = crouchColliderHeight;
        }
        else
        {
            playerCollider.center = new Vector3(playerCollider.center.x, originalColliderCenterY, playerCollider.center.z);
            playerCollider.height = originalColliderHeight;
        }
    }
    void FixedUpdate()
    {
        moveDirection = new Vector3(inputVector.x, 0, inputVector.y);

        // Chức năng mới: khóa Player trong trạng thái action/death độc quyền.
        // Tham chiếu:
        // - PlayerState.IsExclusiveActionState
        // - MissionInteractObjective
        // - PlayerProperties death/failure sequence
        //
        // Khi Player đang Talking / Interacting / StealthAttack / Dead,
        // movement loop cũ không được tự ghi đè animation state về Idle/Running.
        if (PlayerState.Instance != null &&
            PlayerState.Instance.IsExclusiveActionState)
        {
            inputVector = Vector2.zero;
            soundWaveEffect.SetActive(false);

            rb.constraints |=
                RigidbodyConstraints.FreezePositionX |
                RigidbodyConstraints.FreezePositionZ;

            return;
        }

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

    // Chức năng mới: reset runtime Player sau khi restore checkpoint.
    // Tham chiếu:
    // - PlayerProperties.ApplyCheckpointRestore()
    // - CheckpointManager.CheckpointRestoreRequested
    public void ResetAfterCheckpointRestore()
    {
        inputVector = Vector2.zero;
        moveDirection = Vector3.zero;

        isAttacking = false;
        isReadyToAttack = true;
        isEnemyOnAttackRange = false;

        if (isCrouching)
        {
            isCrouching = false;
            UpdateColliderForCrouch(false);
        }

        speed = 5f;
        acceptInput = true;

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            rb.constraints |=
                RigidbodyConstraints.FreezePositionX |
                RigidbodyConstraints.FreezePositionZ;
        }

        if (soundWaveEffect != null)
            soundWaveEffect.SetActive(false);

        if (PlayerCanvasController.Instance != null)
            PlayerCanvasController.Instance.UpdateCrouchIcon(false);
    }

    // Chức năng mới: khóa điều khiển Player từ gameplay action bên ngoài.
    // Tham chiếu: MissionInteractObjective và các action stealth trong tương lai.
    public void SetExternalActionLock(bool locked)
    {
        acceptInput = !locked;

        if (locked)
            inputVector = Vector2.zero;
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
