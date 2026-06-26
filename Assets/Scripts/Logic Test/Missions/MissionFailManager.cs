// Chức năng: Xử lý fail nhẹ khi Player bị phát hiện/chết: khóa điều khiển, fade panel đen, đưa về checkpoint gần nhất, fade out.
// Gán cho: GameManager hoặc object Managers luôn active.
// Tham chiếu với: MissionFlowManager gọi SetCheckpoint(); CameraDetectionZone hoặc script chết/phát hiện gọi FailAndReturnToCheckpoint().
using System.Collections;
using UnityEngine;

public class MissionFailManager : MonoBehaviour
{
    [Header("Fade")]
    [SerializeField] private CanvasGroup blackPanel;
    [SerializeField] private float fadeInDuration = 0.5f;
    [SerializeField] private float fadeOutDuration = 0.5f;
    [SerializeField] private float blackHoldDuration = 0.05f;

    [Header("Player")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private CharacterController playerCharacterController;
    [SerializeField] private Rigidbody playerRigidbody;
    [SerializeField] private Behaviour[] disableDuringFail;

    [Header("Checkpoint")]
    [SerializeField] private Transform currentCheckpoint;

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    private bool isFailing;

    private void Awake()
    {
        if (blackPanel != null)
        {
            blackPanel.alpha = 0f;
            blackPanel.gameObject.SetActive(false);
        }
    }

    public void SetCheckpoint(Transform checkpoint)
    {
        if (checkpoint == null)
            return;

        currentCheckpoint = checkpoint;

        if (logDebug)
            Debug.Log("[MissionFailManager] Checkpoint set: " + checkpoint.name);
    }

    public void FailAndReturnToCheckpoint()
    {
        if (isFailing)
            return;

        StartCoroutine(FailRoutine(currentCheckpoint));
    }

    public void FailAndReturnToCheckpoint(Transform checkpointOverride)
    {
        if (checkpointOverride != null)
            currentCheckpoint = checkpointOverride;

        FailAndReturnToCheckpoint();
    }

    public void TriggerFail()
    {
        FailAndReturnToCheckpoint();
    }

    public void Fail()
    {
        FailAndReturnToCheckpoint();
    }

    public void PlayerFailed()
    {
        FailAndReturnToCheckpoint();
    }

    public void StartFailSequence()
    {
        FailAndReturnToCheckpoint();
    }

    private IEnumerator FailRoutine(Transform checkpoint)
    {
        isFailing = true;
        SetControlledBehaviours(false);

        if (blackPanel != null)
        {
            blackPanel.gameObject.SetActive(true);
            yield return FadeCanvasGroup(blackPanel, 0f, 1f, fadeInDuration);
        }

        if (blackHoldDuration > 0f)
            yield return new WaitForSeconds(blackHoldDuration);

        if (checkpoint != null && playerTransform != null)
            TeleportPlayer(checkpoint.position, checkpoint.rotation);
        else
            Debug.LogWarning("[MissionFailManager] Cannot return to checkpoint because checkpoint or playerTransform is missing.");

        if (blackPanel != null)
        {
            yield return FadeCanvasGroup(blackPanel, 1f, 0f, fadeOutDuration);
            blackPanel.gameObject.SetActive(false);
        }

        SetControlledBehaviours(true);
        isFailing = false;
    }

    private void TeleportPlayer(Vector3 position, Quaternion rotation)
    {
        bool controllerWasEnabled = false;

        if (playerCharacterController != null)
        {
            controllerWasEnabled = playerCharacterController.enabled;
            playerCharacterController.enabled = false;
        }

        if (playerRigidbody != null)
        {
            playerRigidbody.linearVelocity = Vector3.zero;
            playerRigidbody.angularVelocity = Vector3.zero;
        }

        playerTransform.SetPositionAndRotation(position, rotation);

        if (playerCharacterController != null)
            playerCharacterController.enabled = controllerWasEnabled;
    }

    private IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float duration)
    {
        if (group == null)
            yield break;

        if (duration <= 0f)
        {
            group.alpha = to;
            yield break;
        }

        float elapsed = 0f;
        group.alpha = from;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            group.alpha = Mathf.Lerp(from, to, t);
            yield return null;
        }

        group.alpha = to;
    }

    private void SetControlledBehaviours(bool enabled)
    {
        if (disableDuringFail == null)
            return;

        for (int i = 0; i < disableDuringFail.Length; i++)
        {
            if (disableDuringFail[i] != null)
                disableDuringFail[i].enabled = enabled;
        }
    }
}
