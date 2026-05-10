using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;

public class PlayerController : MonoBehaviour
{
    public static PlayerController Instance { get; private set; }

    private Rigidbody rb;
    private Vector2 inputVector;
    private Vector3 moveDirection;
    [SerializeField] private float speed = 5f;

    [HideInInspector] public bool isAttacking = false;
    private bool isReadyToAttack = true;
    private int weaponIndex = 0;
    void Start()
    {
        rb = GetComponent<Rigidbody>();
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
        inputVector = context.ReadValue<Vector2>();
    }
    public void OnAttackInput(InputAction.CallbackContext context)
    {
        if (context.performed && isReadyToAttack)
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
    public void OnWeaponChangeInput(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            weaponIndex = (weaponIndex + 1) % 4; // Giả sử có 4 vũ khí
            PlayerWeapon.WeaponType newWeapon = (PlayerWeapon.WeaponType)weaponIndex;
            PlayerWeapon.Instance.ChangeWeapon(newWeapon);
            PlayerState.Instance.UpdateAnimation();
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
        else
        {
            PlayerState.Instance.CurrentState = PlayerState.State.Idle;
        }

        rb.MovePosition(rb.position + moveDirection * speed * Time.fixedDeltaTime);

        if (moveDirection.sqrMagnitude > 0.01f)
        {
            rb.rotation = Quaternion.LookRotation(moveDirection);
        }
    }
}
