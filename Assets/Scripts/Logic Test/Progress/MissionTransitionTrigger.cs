// Trigger-based black screen transition: turns objects on/off, fades the black UI in, teleports the player at full black, then fades out and hides the UI for reuse.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class MissionBlackScreenTeleportTrigger : MonoBehaviour
{
    [Header("Object State Changes")]
    [SerializeField] private List<GameObject> setActiveTrueObjects = new List<GameObject>();
    [SerializeField] private List<GameObject> setActiveFalseObjects = new List<GameObject>();

    [Header("Black Screen UI")]
    [SerializeField] private GameObject blackCanvasRoot;
    [SerializeField] private CanvasGroup blackPanelGroup;

    [Header("Teleport Target")]
    [SerializeField] private Transform targetPoint;

    [Header("Timing")]
    [SerializeField] private float fadeInDuration = 0.6f;
    [SerializeField] private float holdBlackDuration = 0.5f;
    [SerializeField] private float fadeOutDuration = 0.5f;

    [Header("Options")]
    [SerializeField] private bool disableTriggerAfterUse = true;

    private Collider _triggerCollider;
    private bool _isRunning;
    private Transform _playerRoot;
    private Rigidbody _playerRigidbody;
    private CharacterController _playerCharacterController;

    private void Awake()
    {
        _triggerCollider = GetComponent<Collider>();
        _triggerCollider.isTrigger = true;

        HideBlackUIImmediate();
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleEnter(other);
    }

    private void OnCollisionEnter(Collision collision)
    {
        HandleEnter(collision.collider);
    }

    private void HandleEnter(Collider other)
    {
        if (_isRunning)
            return;

        Transform playerRoot = GetPlayerRoot(other.transform);
        if (playerRoot == null)
            return;

        _playerRoot = playerRoot;
        CachePlayerComponents(_playerRoot);

        ApplyObjectStates();

        StartCoroutine(BlackTeleportRoutine());
    }

    private Transform GetPlayerRoot(Transform hitTransform)
    {
        if (hitTransform == null)
            return null;

        if (hitTransform.CompareTag("Player"))
            return hitTransform;

        Transform root = hitTransform.root;
        if (root != null && root.CompareTag("Player"))
            return root;

        return null;
    }

    private void CachePlayerComponents(Transform playerRoot)
    {
        _playerRigidbody = null;
        _playerCharacterController = null;

        if (playerRoot == null)
            return;

        _playerRigidbody = playerRoot.GetComponent<Rigidbody>();
        _playerCharacterController = playerRoot.GetComponent<CharacterController>();
    }

    private void ApplyObjectStates()
    {
        if (setActiveTrueObjects != null)
        {
            for (int i = 0; i < setActiveTrueObjects.Count; i++)
            {
                if (setActiveTrueObjects[i] != null)
                    setActiveTrueObjects[i].SetActive(true);
            }
        }

        if (setActiveFalseObjects != null)
        {
            for (int i = 0; i < setActiveFalseObjects.Count; i++)
            {
                if (setActiveFalseObjects[i] != null)
                    setActiveFalseObjects[i].SetActive(false);
            }
        }
    }

    private IEnumerator BlackTeleportRoutine()
    {
        _isRunning = true;

        SetBlackUIActive(true);
        yield return FadePanel(0f, 1f, fadeInDuration);

        TeleportPlayerToTarget();

        yield return new WaitForSeconds(holdBlackDuration);

        yield return FadePanel(1f, 0f, fadeOutDuration);
        HideBlackUIImmediate();

        if (disableTriggerAfterUse && _triggerCollider != null)
            _triggerCollider.enabled = false;

        _playerRoot = null;
        _playerRigidbody = null;
        _playerCharacterController = null;
        _isRunning = false;
    }

    private void TeleportPlayerToTarget()
    {
        if (_playerRoot == null || targetPoint == null)
            return;

        if (_playerCharacterController != null)
            _playerCharacterController.enabled = false;

        if (_playerRigidbody != null)
        {
            _playerRigidbody.linearVelocity = Vector3.zero;
            _playerRigidbody.angularVelocity = Vector3.zero;
            _playerRigidbody.position = targetPoint.position;
            _playerRigidbody.rotation = targetPoint.rotation;
        }

        _playerRoot.SetPositionAndRotation(targetPoint.position, targetPoint.rotation);

        Physics.SyncTransforms();

        if (_playerCharacterController != null)
            _playerCharacterController.enabled = true;
    }

    private IEnumerator FadePanel(float from, float to, float duration)
    {
        if (blackPanelGroup == null)
            yield break;

        if (blackCanvasRoot != null)
            blackCanvasRoot.SetActive(true);
        else
            blackPanelGroup.gameObject.SetActive(true);

        blackPanelGroup.alpha = from;

        if (duration <= 0f)
        {
            blackPanelGroup.alpha = to;
            yield break;
        }

        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / duration);
            blackPanelGroup.alpha = Mathf.Lerp(from, to, t);
            yield return null;
        }

        blackPanelGroup.alpha = to;
    }

    private void SetBlackUIActive(bool active)
    {
        if (blackCanvasRoot != null)
            blackCanvasRoot.SetActive(active);

        if (blackPanelGroup != null)
        {
            blackPanelGroup.gameObject.SetActive(active);
            blackPanelGroup.alpha = 0f;
        }
    }

    private void HideBlackUIImmediate()
    {
        if (blackPanelGroup != null)
        {
            blackPanelGroup.alpha = 0f;
            blackPanelGroup.gameObject.SetActive(false);
        }

        if (blackCanvasRoot != null)
            blackCanvasRoot.SetActive(false);
    }
}