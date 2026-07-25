// Chức năng: Điều khiển CameraFollowTarget của Cinemachine bay tới điểm giới thiệu, focus target theo hội thoại, rồi quay lại Player khi flow yêu cầu.
// BẢN NÂNG CẤP:
// - Giữ hàm PlayCameraShot(target, moveTime, holdTime) cũ để không phá logic cũ.
// - Thêm BeginCameraControl/EndCameraControl dạng lock counter để camera/dialogue không mở control sai thời điểm.
// - Thêm MoveToTarget(), ReturnToPlayer(), PlayCameraShotWithConversation().
// - Nếu CameraShot có hội thoại sau khi tới target, camera sẽ đợi hội thoại kết thúc rồi mới return.
using System.Collections;
using UnityEngine;

public class MissionCameraDirector : MonoBehaviour
{
    [Header("Targets")]
    [SerializeField] private Transform cameraFollowTarget;
    [SerializeField] private Transform playerTarget;
    [SerializeField] private Vector3 playerFollowOffset;

    [Header("Control Lock")]
    [SerializeField] private Behaviour[] disableDuringCameraIntro;
    [SerializeField] private PlayerInputLockController inputLockController;
    [SerializeField] private string inputLockReason = "Camera";

    [Header("Settings")]
    [SerializeField] private float returnMoveTime = 0.75f;
    [SerializeField] private bool keepFollowingPlayerWhenIdle = true;

    [Header("Debug")]
    [SerializeField] private bool logDebug;

    private int cameraControlLockCount;
    private bool isMoving;

    public bool IsCameraControlled => cameraControlLockCount > 0;
    public bool IsMoving => isMoving;

    private void Awake()
    {
        if (inputLockController == null)
            inputLockController = FindFirstObjectByType<PlayerInputLockController>();
    }

    private void LateUpdate()
    {
        if (!keepFollowingPlayerWhenIdle)
            return;

        if (IsCameraControlled || isMoving)
            return;

        if (cameraFollowTarget == null || playerTarget == null)
            return;

        cameraFollowTarget.position = playerTarget.position + playerFollowOffset;
    }

    // Hàm cũ: vẫn hoạt động như trước.
    public IEnumerator PlayCameraShot(Transform targetPoint, float moveTime, float holdTime)
    {
        if (cameraFollowTarget == null || playerTarget == null || targetPoint == null)
            yield break;

        BeginCameraControl();

        yield return MoveToTarget(targetPoint, moveTime);

        if (holdTime > 0f)
            yield return new WaitForSeconds(holdTime);

        yield return ReturnToPlayer(returnMoveTime);

        EndCameraControl();
    }

    // Dùng cho case: camera lia tới target, đợi 1s, bật hội thoại, hội thoại xong mới return player.
    public IEnumerator PlayCameraShotWithConversation(
        Transform targetPoint,
        float moveTime,
        float holdTime,
        DialogueConversationData conversationAfterArrive,
        DialogueController dialogueController,
        float delayBeforeConversation,
        bool returnToPlayerAfterShot)
    {
        if (cameraFollowTarget == null || playerTarget == null || targetPoint == null)
            yield break;

        BeginCameraControl();

        yield return MoveToTarget(targetPoint, moveTime);

        if (conversationAfterArrive != null && dialogueController != null)
        {
            if (delayBeforeConversation > 0f)
                yield return new WaitForSeconds(delayBeforeConversation);

            yield return dialogueController.PlayConversationRoutine(conversationAfterArrive);
        }
        else if (holdTime > 0f)
        {
            yield return new WaitForSeconds(holdTime);
        }

        if (returnToPlayerAfterShot)
            yield return ReturnToPlayer(returnMoveTime);

        EndCameraControl();
    }

    public void BeginCameraControl()
    {
        cameraControlLockCount++;

        if (cameraControlLockCount == 1)
        {
            SetControlledBehaviours(false);
            inputLockController?.Lock(inputLockReason);
        }

        if (logDebug)
            Debug.Log("[MissionCameraDirector] Begin camera control. Count = " + cameraControlLockCount, this);
    }

    public void EndCameraControl()
    {
        cameraControlLockCount = Mathf.Max(0, cameraControlLockCount - 1);

        if (cameraControlLockCount == 0)
        {
            SetControlledBehaviours(true);
            inputLockController?.Unlock(inputLockReason);
        }

        if (logDebug)
            Debug.Log("[MissionCameraDirector] End camera control. Count = " + cameraControlLockCount, this);
    }

    public IEnumerator MoveToTarget(Transform targetPoint, float duration)
    {
        if (targetPoint == null)
            yield break;

        yield return MoveToPosition(targetPoint.position, duration);
    }

    public IEnumerator ReturnToPlayer(float duration = -1f)
    {
        if (playerTarget == null)
            yield break;

        float finalDuration = duration >= 0f ? duration : returnMoveTime;
        Vector3 playerPosition = playerTarget.position + playerFollowOffset;
        yield return MoveToPosition(playerPosition, finalDuration);
    }

    public void SnapToPlayer()
    {
        if (cameraFollowTarget == null || playerTarget == null)
            return;

        cameraFollowTarget.position = playerTarget.position + playerFollowOffset;
    }

    private IEnumerator MoveToPosition(Vector3 targetPosition, float duration)
    {
        if (cameraFollowTarget == null)
            yield break;

        isMoving = true;

        Vector3 startPosition = cameraFollowTarget.position;
        float elapsed = 0f;

        if (duration <= 0f)
        {
            cameraFollowTarget.position = targetPosition;
            isMoving = false;
            yield break;
        }

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = Smooth01(t);
            cameraFollowTarget.position = Vector3.LerpUnclamped(startPosition, targetPosition, t);
            yield return null;
        }

        cameraFollowTarget.position = targetPosition;
        isMoving = false;
    }

    private void SetControlledBehaviours(bool enabled)
    {
        if (disableDuringCameraIntro == null)
            return;

        for (int i = 0; i < disableDuringCameraIntro.Length; i++)
        {
            if (disableDuringCameraIntro[i] != null)
                disableDuringCameraIntro[i].enabled = enabled;
        }
    }

    private float Smooth01(float t)
    {
        return t * t * (3f - 2f * t);
    }
}
