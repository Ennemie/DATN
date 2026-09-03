// Chức năng: Runtime authority cho Checkpoint System + integration với Persistent Data.
//
// Architecture:
// - CheckpointManager giữ runtime checkpoint.
// - GameDataManager giữ persistent data.
// - Continue flow gọi ApplySavedCheckpoint() sau khi scene đã load.
// - Player/Enemy/Mission phase sau sẽ subscribe vào CheckpointRestoreRequested
//   và PersistentCheckpointApplied để restore state của domain tương ứng.
//
// Lưu ý:
// - Checkpoint Transform KHÔNG được lưu vào JSON.
// - Khi Continue, Transform được resolve lại từ MissionElementTrigger trong scene.
// - Không tự Load save trong Awake/Start.
// - Không save trong Update().

using System;
using UnityEngine;

public class CheckpointManager : MonoBehaviour
{
    [Serializable]
    public struct CheckpointState
    {
        public int MapNumber;
        public int CheckpointNumber;
        public Transform CheckpointTransform;
        public string Message;

        // Chức năng mới:
        // Hai list này là runtime-only restore instructions của checkpoint.
        // Chúng không được ghi vào JSON.
        public GameObject[] RestoreActiveObjects;
        public GameObject[] RestoreInactiveObjects;

        public CheckpointState(
            int mapNumber,
            int checkpointNumber,
            Transform checkpointTransform,
            string message,
            GameObject[] restoreActiveObjects = null,
            GameObject[] restoreInactiveObjects = null)
        {
            MapNumber = mapNumber;
            CheckpointNumber = checkpointNumber;
            CheckpointTransform = checkpointTransform;
            Message = message;
            RestoreActiveObjects = restoreActiveObjects;
            RestoreInactiveObjects = restoreInactiveObjects;
        }
    }

    [Header("Data")]
    [SerializeField] private GameDataManager dataManager;

    // Chức năng mới:
    // CheckpointManager là authority duy nhất của feedback Checkpoint.
    // Nhờ vậy mọi checkpoint trigger dùng chung một Toast.
    [Header("Checkpoint UI")]
    [SerializeField] private MissionCheckpointToast checkpointToast;

    [Header("Current Runtime Checkpoint")]
    [SerializeField] private int currentMap = 0;
    [SerializeField] private int currentCheckpoint = 0;
    [SerializeField] private Transform currentCheckpointTransform;
    [SerializeField] private string currentCheckpointMessage = "Checkpoint";

    // Chức năng mới:
    // Giữ lại hai list của checkpoint hiện tại để RestoreCheckpoint()
    // có thể áp dụng đúng setup object của checkpoint.
    private GameObject[] currentCheckpointRestoreActiveObjects;
    private GameObject[] currentCheckpointRestoreInactiveObjects;

    [Tooltip(
        "Nếu bật, khi Continue khôi phục CP đã commit, tất cả checkpoint " +
        "cũ hơn hoặc bằng CP đó sẽ bị consume/inactive trong scene."
    )]
    [SerializeField] private bool consumePreviousCheckpointTriggersOnContinue = true;

    public event Action<CheckpointState> CheckpointReached;

    /// <summary>
    /// Player death gọi event này để Player/Enemy/Mission restore runtime state.
    /// GameDataManager không trực tiếp teleport hay reset object.
    /// </summary>
    public event Action<CheckpointState> CheckpointRestoreRequested;

    /// <summary>
    /// Continue flow: saved checkpoint đã được resolve thành runtime checkpoint.
    /// </summary>
    public event Action<CheckpointState> PersistentCheckpointApplied;

    public int CurrentMap => currentMap;
    public int CurrentCheckpoint => currentCheckpoint;
    public Transform CurrentCheckpointTransform => currentCheckpointTransform;
    public string CurrentCheckpointMessage => currentCheckpointMessage;

    public bool HasCheckpoint =>
        currentCheckpointTransform != null &&
        currentMap > 0 &&
        currentCheckpoint > 0;

    private void Awake()
    {
        ResolveDataManager();
        ResolveCheckpointToast();
    }

    public void SetDataManager(GameDataManager manager)
    {
        dataManager = manager;
    }

    /// <summary>
    /// Đăng ký checkpoint mới.
    /// Runtime state được cập nhật trước, sau đó commit Map + Checkpoint vào DataManager.
    /// </summary>
    public bool ReachCheckpoint(
        int mapNumber,
        int checkpointNumber,
        Transform checkpointTransform,
        string message = "Checkpoint",
        GameObject[] restoreActiveObjects = null,
        GameObject[] restoreInactiveObjects = null)
    {
        if (mapNumber < 1)
        {
            Debug.LogWarning(
                "[CheckpointManager] Invalid Map Number: " + mapNumber,
                this
            );
            return false;
        }

        if (checkpointNumber < 1)
        {
            Debug.LogWarning(
                "[CheckpointManager] Invalid Checkpoint Number: " + checkpointNumber,
                this
            );
            return false;
        }

        if (checkpointTransform == null)
        {
            Debug.LogWarning(
                "[CheckpointManager] Checkpoint Transform is missing. " +
                "Map = " + mapNumber +
                ", Checkpoint = " + checkpointNumber,
                this
            );
            return false;
        }

        if (!IsCheckpointProgressionValid(mapNumber, checkpointNumber))
        {
            Debug.Log(
                "[CheckpointManager] Ignored checkpoint because it is not newer than current checkpoint. " +
                "Current = M" + currentMap + " CP" + currentCheckpoint +
                ", Requested = M" + mapNumber + " CP" + checkpointNumber,
                this
            );
            return false;
        }

        currentMap = mapNumber;
        currentCheckpoint = checkpointNumber;
        currentCheckpointTransform = checkpointTransform;
        currentCheckpointMessage = string.IsNullOrWhiteSpace(message)
            ? "Checkpoint"
            : message;

        // Chức năng mới:
        // Nếu Trigger truyền list trực tiếp thì dùng đúng list đó.
        // Nếu code cũ gọi ReachCheckpoint() mà không truyền list,
        // Manager cố gắng resolve checkpoint trigger theo Map/Checkpoint để
        // giữ compatibility.
        ResolveCheckpointRestoreObjectLists(
            mapNumber,
            checkpointNumber,
            restoreActiveObjects,
            restoreInactiveObjects
        );

        CheckpointState state = GetCurrentState();

        ResolveDataManager();

        bool persistenceSucceeded = false;

        if (dataManager != null)
        {
            // Capture the Mission runtime snapshot BEFORE writing the checkpoint
            // so Mission and Checkpoint become one persistent commit.
            MissionFlowManager missionFlowManager =
                FindAnyObjectByType<MissionFlowManager>();

            if (missionFlowManager != null)
            {
                missionFlowManager.CapturePersistentState(
                    dataManager.Data
                );
            }

            // =========================================================================
            // MỚI - PLAYER / ENEMY CHECKPOINT SNAPSHOT
            // ----------------------------------------------------------------------------
            // PlayerHealth là trạng thái Player tại thời điểm commit checkpoint.
            // Enemy nào đang chết tại thời điểm checkpoint sẽ trở thành Permanent Dead.
            // Không lưu Transform/AI state của Enemy vào JSON.
            // =========================================================================
            PlayerProperties playerProperties =
                FindAnyObjectByType<PlayerProperties>();

            if (playerProperties != null)
            {
                dataManager.SetPlayerHealth(
                    playerProperties.CurrentHealth
                );
            }
            else
            {
                Debug.LogWarning(
                    "[CheckpointManager] PlayerProperties was not found while committing checkpoint. " +
                    "PlayerHealth will keep its previous saved value.",
                    this
                );
            }

            EnemyCheckpointHandler[] enemyHandlers =
                FindObjectsByType<EnemyCheckpointHandler>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None
                );

            for (int i = 0; i < enemyHandlers.Length; i++)
            {
                EnemyCheckpointHandler enemyHandler = enemyHandlers[i];

                if (enemyHandler == null ||
                    !enemyHandler.ShouldCommitAsPermanentDeath)
                {
                    continue;
                }

                string enemyId = enemyHandler.PersistentEnemyId;

                if (!string.IsNullOrWhiteSpace(enemyId))
                {
                    dataManager.AddPermanentDeadEnemy(
                        enemyId,
                        false
                    );
                }
            }

            persistenceSucceeded = dataManager.CommitCheckpoint(
                currentMap,
                currentCheckpoint
            );

            if (!persistenceSucceeded)
            {
                Debug.LogError(
                    "[CheckpointManager] Checkpoint runtime state changed, " +
                    "but persistent save failed. Runtime and disk state are now out of sync.",
                    this
                );

                return false;
            }
        }
        else
        {
            Debug.LogError(
                "[CheckpointManager] GameDataManager is missing. " +
                "Checkpoint commit was rejected because persistent data is required.",
                this
            );

            return false;
        }

        Debug.Log(
            "[CheckpointManager] Checkpoint committed. " +
            "Map = " + currentMap +
            ", Checkpoint = " + currentCheckpoint +
            ", Transform = " + currentCheckpointTransform.name,
            currentCheckpointTransform
        );

        // Chức năng mới:
        // Chỉ hiện Toast sau khi commit save thành công.
        ResolveCheckpointToast();

        if (checkpointToast != null)
            checkpointToast.ShowCheckpoint(currentCheckpointMessage);

        CheckpointReached?.Invoke(state);
        return true;
    }

    /// <summary>
    /// Player death gọi hàm này.
    /// Player/Enemy/Mission sẽ nhận event và tự restore state của chúng.
    /// </summary>
    public bool RestoreCheckpoint()
    {
        if (!HasCheckpoint)
        {
            Debug.LogWarning(
                "[CheckpointManager] Restore requested, but no valid runtime checkpoint exists.",
                this
            );
            return false;
        }

        CheckpointState state = GetCurrentState();

        Debug.Log(
            "[CheckpointManager] Runtime checkpoint restore requested. " +
            "Map = " + currentMap +
            ", Checkpoint = " + currentCheckpoint +
            ", Transform = " + currentCheckpointTransform.name,
            currentCheckpointTransform
        );

        // Chức năng mới:
        // Domain restore (Player/Enemy/Mission) chạy trước.
        // Sau khi các domain đã restore xong, CheckpointManager mới áp dụng
        // activation state của checkpoint. Như vậy:
        // Enemy có thể restore baseline trước, sau đó bị SetActive(false)
        // đúng theo flow checkpoint; ConversationTrigger có thể được bật lại
        // và chờ lần kích hoạt tiếp theo.
        CheckpointRestoreRequested?.Invoke(state);

        ApplyCheckpointRestoreObjectState(state);

        return true;
    }

    /// <summary>
    /// Chỉ dùng cho Continue flow.
    /// Không tự gọi khi scene load bình thường.
    /// </summary>
    public bool ApplySavedCheckpoint(GameSaveData loadedData)
    {
        if (loadedData == null)
        {
            Debug.LogWarning(
                "[CheckpointManager] ApplySavedCheckpoint received null data.",
                this
            );
            return false;
        }

        loadedData.EnsureLists();

        if (!loadedData.HasCommittedCheckpoint)
        {
            Debug.LogWarning(
                "[CheckpointManager] Save data does not contain a valid checkpoint. " +
                "Map = " + loadedData.CurrentMap +
                ", Checkpoint = " + loadedData.CurrentCheckpoint,
                this
            );
            return false;
        }

        Transform resolvedTransform = ResolveCheckpointTransform(
            loadedData.CurrentMap,
            loadedData.CurrentCheckpoint
        );

        if (resolvedTransform == null)
        {
            Debug.LogError(
                "[CheckpointManager] Could not resolve saved checkpoint in loaded scene. " +
                "Map = " + loadedData.CurrentMap +
                ", Checkpoint = " + loadedData.CurrentCheckpoint,
                this
            );
            return false;
        }

        currentMap = loadedData.CurrentMap;
        currentCheckpoint = loadedData.CurrentCheckpoint;
        currentCheckpointTransform = resolvedTransform;
        currentCheckpointMessage = "Checkpoint";

        // Chức năng mới:
        // Continue không có transform/list trong JSON. Resolve lại object setup
        // từ chính MissionElementTrigger của checkpoint trong scene đang load.
        ResolveCheckpointRestoreObjectLists(
            currentMap,
            currentCheckpoint,
            null,
            null
        );

        if (consumePreviousCheckpointTriggersOnContinue)
            ApplyCheckpointTriggerGates(
                currentMap,
                currentCheckpoint
            );

        CheckpointState state = GetCurrentState();

        Debug.Log(
            "[CheckpointManager] Saved checkpoint applied to runtime. " +
            "Map = " + currentMap +
            ", Checkpoint = " + currentCheckpoint +
            ", Transform = " + currentCheckpointTransform.name,
            currentCheckpointTransform
        );

        // Chức năng mới:
        // Continue restore cũng áp dụng object activation sau khi Player/Enemy/Mission
        // đã nhận event restore.
        PersistentCheckpointApplied?.Invoke(state);

        ApplyCheckpointRestoreObjectState(state);

        return true;
    }

    public bool TryGetCurrentCheckpointTransform(
        out Transform checkpointTransform)
    {
        checkpointTransform = currentCheckpointTransform;
        return HasCheckpoint;
    }

    public bool IsCurrentCheckpoint(int mapNumber, int checkpointNumber)
    {
        return currentMap == mapNumber &&
               currentCheckpoint == checkpointNumber;
    }

    public CheckpointState GetCurrentState()
    {
        return new CheckpointState(
            currentMap,
            currentCheckpoint,
            currentCheckpointTransform,
            currentCheckpointMessage,
            currentCheckpointRestoreActiveObjects,
            currentCheckpointRestoreInactiveObjects
        );
    }

    /// <summary>
    /// Chỉ clear runtime state. Không xóa save.json.
    /// </summary>
    public void ClearRuntimeCheckpoint()
    {
        currentMap = 0;
        currentCheckpoint = 0;
        currentCheckpointTransform = null;
        currentCheckpointMessage = "Checkpoint";

        Debug.Log(
            "[CheckpointManager] Runtime checkpoint cleared.",
            this
        );
    }

    private Transform ResolveCheckpointTransform(
        int mapNumber,
        int checkpointNumber)
    {
        MissionElementTrigger[] checkpointTriggers =
            FindObjectsByType<MissionElementTrigger>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        for (int i = 0; i < checkpointTriggers.Length; i++)
        {
            MissionElementTrigger trigger = checkpointTriggers[i];

            if (trigger == null ||
                trigger.Action != MissionElementTrigger.TriggerAction.SaveCheckpointOnly)
            {
                continue;
            }

            if (trigger.MapNumber != mapNumber ||
                trigger.CheckpointNumber != checkpointNumber)
            {
                continue;
            }

            return trigger.CheckpointPoint != null
                ? trigger.CheckpointPoint
                : trigger.transform;
        }

        // Điều chỉnh:
        // Giữ tương thích với scene còn dùng MissionCheckpointTrigger riêng.
        MissionCheckpointTrigger[] legacyCheckpointTriggers =
            FindObjectsByType<MissionCheckpointTrigger>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        for (int i = 0; i < legacyCheckpointTriggers.Length; i++)
        {
            MissionCheckpointTrigger trigger = legacyCheckpointTriggers[i];

            if (trigger == null)
                continue;

            if (trigger.MapNumber != mapNumber ||
                trigger.CheckpointNumber != checkpointNumber)
            {
                continue;
            }

            return trigger.CheckpointPoint != null
                ? trigger.CheckpointPoint
                : trigger.transform;
        }

        return null;
    }

    // Chức năng mới:
    // Resolve hai list Object activation của checkpoint.
    //
    // Ưu tiên:
    // 1) list được MissionElementTrigger truyền trực tiếp;
    // 2) nếu không có, tìm MissionElementTrigger theo Map + Checkpoint.
    //
    // Hai list này không lưu vào GameSaveData; chúng là setup tĩnh của scene.
    private void ResolveCheckpointRestoreObjectLists(
        int mapNumber,
        int checkpointNumber,
        GameObject[] restoreActiveObjects,
        GameObject[] restoreInactiveObjects)
    {
        if (restoreActiveObjects != null ||
            restoreInactiveObjects != null)
        {
            currentCheckpointRestoreActiveObjects =
                restoreActiveObjects ?? System.Array.Empty<GameObject>();

            currentCheckpointRestoreInactiveObjects =
                restoreInactiveObjects ?? System.Array.Empty<GameObject>();

            return;
        }

        MissionElementTrigger[] triggers =
            FindObjectsByType<MissionElementTrigger>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        for (int i = 0; i < triggers.Length; i++)
        {
            MissionElementTrigger trigger = triggers[i];

            if (trigger == null ||
                trigger.Action != MissionElementTrigger.TriggerAction.SaveCheckpointOnly)
            {
                continue;
            }

            if (trigger.MapNumber != mapNumber ||
                trigger.CheckpointNumber != checkpointNumber)
            {
                continue;
            }

            currentCheckpointRestoreActiveObjects =
                trigger.CheckpointRestoreActiveObjects ??
                System.Array.Empty<GameObject>();

            currentCheckpointRestoreInactiveObjects =
                trigger.CheckpointRestoreInactiveObjects ??
                System.Array.Empty<GameObject>();

            return;
        }

        currentCheckpointRestoreActiveObjects =
            System.Array.Empty<GameObject>();

        currentCheckpointRestoreInactiveObjects =
            System.Array.Empty<GameObject>();
    }

    // Chức năng mới:
    // Thực thi activation/deactivation sau khi Checkpoint restore event đã chạy.
    // Khi bật ConversationTrigger, reset runtime gate trước để trigger one-time
    // có thể chạy lại trong vòng đời checkpoint.
    private void ApplyCheckpointRestoreObjectState(
        CheckpointState state)
    {
        ApplySetActiveWithCheckpointPreparation(
            state.RestoreInactiveObjects,
            false
        );

        ApplySetActiveWithCheckpointPreparation(
            state.RestoreActiveObjects,
            true
        );
    }

    private void ApplySetActiveWithCheckpointPreparation(
        GameObject[] objects,
        bool active)
    {
        if (objects == null)
            return;

        for (int i = 0; i < objects.Length; i++)
        {
            GameObject target = objects[i];

            if (target == null)
                continue;

            if (!active)
            {
                target.SetActive(false);
                continue;
            }

            // Chức năng mới:
            // ConversationTrigger có hasTriggered runtime riêng.
            // Bật GameObject đơn thuần không reset cờ oneTime,
            // nên phải reset trước khi SetActive(true).
            ConversationTrigger conversationTrigger =
                target.GetComponent<ConversationTrigger>();

            if (conversationTrigger != null)
                conversationTrigger.ResetRuntimeTriggerState();

            // Checkpoint Trigger cũng có runtime gate riêng.
            MissionElementTrigger missionTrigger =
                target.GetComponent<MissionElementTrigger>();

            if (missionTrigger != null)
                missionTrigger.ResetCheckpointRuntimeGate();

            target.SetActive(true);
        }
    }

    private void ApplyCheckpointTriggerGates(
        int savedMap,
        int savedCheckpoint)
    {
        MissionElementTrigger[] checkpointTriggers =
            FindObjectsByType<MissionElementTrigger>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        for (int i = 0; i < checkpointTriggers.Length; i++)
        {
            MissionElementTrigger trigger = checkpointTriggers[i];

            if (trigger == null)
                continue;

            trigger.ApplyPersistentCheckpointGate(
                savedMap,
                savedCheckpoint,
                true
            );
        }
    }

    // Chức năng mới:
    // Tự tìm CheckpointToast kể cả khi visual root của Toast đang inactive.
    private void ResolveCheckpointToast()
    {
        if (checkpointToast != null)
            return;

        MissionCheckpointToast[] toasts =
            FindObjectsByType<MissionCheckpointToast>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        if (toasts != null && toasts.Length > 0)
            checkpointToast = toasts[0];
    }

    private void ResolveDataManager()
    {
        if (dataManager != null)
            return;

        dataManager = GameDataManager.Instance;

        if (dataManager == null)
            dataManager = FindAnyObjectByType<GameDataManager>();
    }

    private bool IsCheckpointProgressionValid(
        int mapNumber,
        int checkpointNumber)
    {
        if (!HasCheckpoint)
            return true;

        if (mapNumber > currentMap)
            return true;

        if (mapNumber < currentMap)
            return false;

        return checkpointNumber > currentCheckpoint;
    }
}
