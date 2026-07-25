// ==============================
// MissionFailManager.cs
// ==============================
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
    [SerializeField] private PlayerLaserReceiver playerLaserReceiver;

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

        CacheReferences();
        Debug.Log(
    "[MissionFailManager] Awake\n" +
    "CurrentCheckpoint = " +
    (currentCheckpoint != null ? currentCheckpoint.name : "NULL")
);
    }

    private void CacheReferences()
    {
        if (playerLaserReceiver == null)
            playerLaserReceiver = FindFirstObjectByType<PlayerLaserReceiver>();

        if (playerTransform == null && playerLaserReceiver != null)
            playerTransform = playerLaserReceiver.transform;

        if (playerTransform != null && playerCharacterController == null)
            playerCharacterController = playerTransform.GetComponent<CharacterController>();

        if (playerTransform != null && playerRigidbody == null)
            playerRigidbody = playerTransform.GetComponent<Rigidbody>();
    }

    public void SetCheckpoint(Transform checkpoint)
    {
        SetCheckpoint(checkpoint, "Checkpoint");
        Debug.Log(
    "[MissionFailManager] Checkpoint set: " +
    checkpoint.name +
    " | Pos = " +
    checkpoint.position,
    this
);
    }

    public void SetCheckpoint(Transform checkpoint, string message)
    {
        if (checkpoint == null)
            return;

        currentCheckpoint = checkpoint;

        Debug.Log(
            "[MissionFailManager] CHECKPOINT SAVED\n" +
            "Object = " + checkpoint.name +
            "\nMessage = " + message +
            "\nStack:\n" +
            System.Environment.StackTrace,
            checkpoint
        );
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
        Debug.Log("[MissionFailManager] FailRoutine START");
        Debug.Log("[MissionFailManager] Checkpoint = " + (checkpoint != null ? checkpoint.name : "NULL"));
        Debug.Log("[MissionFailManager] Player = " + (playerTransform != null ? playerTransform.name : "NULL"));
        Debug.Log(
    "[MissionFailManager] Current checkpoint position = " +
    checkpoint.position
);
        Debug.Log("[MissionFailManager] ===== FAIL START =====");

        if (currentCheckpoint != null)
        {
            Debug.Log(
                "[MissionFailManager] Current Checkpoint Object = " +
                currentCheckpoint.gameObject.name +
                "\nHierarchy = " +
                GetHierarchyPath(currentCheckpoint) +
                "\nPosition = " +
                currentCheckpoint.position,
                currentCheckpoint.gameObject
            );
        }
        else
        {
            Debug.LogWarning("[MissionFailManager] Current Checkpoint = NULL");
        }
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
        {
            Debug.Log("[MissionFailManager] Teleport -> " + checkpoint.position);
            TeleportPlayer(checkpoint.position, checkpoint.rotation);
        }
        else
        {
            Debug.LogWarning("[MissionFailManager] Teleport skipped");
        }
        //else if (logDebug)
        //    Debug.LogWarning("[MissionFailManager] Cannot return to checkpoint because checkpoint or playerTransform is missing.", this);

        ResetLaserStateAfterRespawn();

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
        Debug.Log("[MissionFailManager] Before Teleport = " + playerTransform.position);
        if (playerCharacterController != null)
            playerCharacterController.enabled = controllerWasEnabled;
        StartCoroutine(CheckPositionNextFrame());
    }
    private IEnumerator CheckPositionNextFrame()
    {
        yield return null;

        Debug.Log("[MissionFailManager] Position next frame = " + playerTransform.position);
    }
    private void ResetLaserStateAfterRespawn()
    {
        if (playerLaserReceiver != null)
            playerLaserReceiver.ResetLaserState();

        LaserDetectionZone[] zones = FindObjectsByType<LaserDetectionZone>(FindObjectsSortMode.None);
        for (int i = 0; i < zones.Length; i++)
        {
            if (zones[i] != null)
                zones[i].ResetDetection();
        }
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
    private string GetHierarchyPath(Transform target)
    {
        if (target == null)
            return "NULL";

        string path = target.name;

        while (target.parent != null)
        {
            target = target.parent;
            path = target.name + "/" + path;
        }

        return path;
    }
}
