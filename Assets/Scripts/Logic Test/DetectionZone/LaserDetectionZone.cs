using UnityEngine;

[RequireComponent(typeof(Collider))]
public class LaserDetectionZone : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private bool useDetectionDelay = false;
    [SerializeField] private float detectionTime = 0f;

    [Header("Fail")]
    [SerializeField] private MissionFailManager missionFailManager;
    [SerializeField] private PlayerLaserReceiver playerLaserReceiver;
    [SerializeField] private bool failWhenDetected = true;

    [Header("Runtime (Read Only)")]
    [SerializeField] private bool playerDetected = false;
    [SerializeField] private bool playerLost = false;
    [SerializeField] private float timer = 0f;

    public bool PlayerDetected => playerDetected;
    public bool PlayerLost => playerLost;

    private Collider zoneCollider;

    private void Reset()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;
    }

    private void Awake()
    {
        zoneCollider = GetComponent<Collider>();
        if (zoneCollider != null)
            zoneCollider.isTrigger = true;

        if (missionFailManager == null)
            missionFailManager = FindFirstObjectByType<MissionFailManager>();

        if (playerLaserReceiver == null)
            playerLaserReceiver = FindFirstObjectByType<PlayerLaserReceiver>();
    }

    private void Update()
    {
        if (playerLost || !playerDetected)
            return;

        if (!useDetectionDelay)
        {
            TriggerDetected();
            return;
        }

        timer -= Time.deltaTime;

        if (timer <= 0f)
        {
            timer = 0f;
            TriggerDetected();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other))
            return;

        if (playerLost)
            return;

        playerDetected = true;

        if (useDetectionDelay)
        {
            timer = detectionTime;
        }
        else
        {
            TriggerDetected();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayer(other))
            return;

        if (playerLost)
            return;

        playerDetected = false;
        timer = 0f;
    }

    private void TriggerDetected()
    {
        if (playerLost)
            return;

        playerDetected = false;
        playerLost = true;
        timer = 0f;

        if (playerLaserReceiver != null)
            playerLaserReceiver.SetDetected(true);

        if (failWhenDetected && missionFailManager != null)
            missionFailManager.FailAndReturnToCheckpoint();
    }

    public void ResetDetection()
    {
        playerDetected = false;
        playerLost = false;
        timer = 0f;
    }

    private bool IsPlayer(Collider other)
    {
        if (other == null)
            return false;

        if (other.CompareTag("Player"))
            return true;

        Transform root = other.transform.root;
        return root != null && root.CompareTag("Player");
    }
}
