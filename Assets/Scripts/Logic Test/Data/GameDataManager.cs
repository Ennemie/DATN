// Chức năng: Persistent Data Manager.
// - Được đặt ở Scene Menu và DontDestroyOnLoad.
// - Quản lý GameSaveData trong runtime.
// - Load/Save một file JSON duy nhất tại Application.persistentDataPath.
// - KHÔNG tự Load save khi Awake.
// - Continue từ Menu mới chủ động gọi LoadFromDisk().
// - Tutorial không cần setup DataManager riêng; instance từ Menu sẽ đi xuyên scene.
// - CheckpointManager gọi CommitCheckpoint() tại checkpoint commit point.

using System;
using System.Collections.Generic;
using UnityEngine;

public class GameDataManager : MonoBehaviour
{
    public static GameDataManager Instance { get; private set; }

    [Header("Save File")]
    [SerializeField] private string saveFileName = "save.json";
    [SerializeField] private bool dontDestroyOnLoad = true;

    private GameSaveData saveData;
    private bool continueRestorePending;

    public event Action<GameSaveData> SaveLoaded;
    public event Action<GameSaveData> SaveWritten;
    public event Action SaveCleared;
    public event Action<GameSaveData> ContinueRestorePrepared;
    public event Action ContinueRestoreConsumed;

    public GameSaveData Data
    {
        get
        {
            EnsureRuntimeData();
            return saveData;
        }
    }

    public string SaveFilePath => GameDataSaver.GetSaveFilePath(saveFileName);
    public bool HasSaveFile => GameDataSaver.Exists(saveFileName);

    public bool HasValidSaveData
    {
        get
        {
            return saveData != null && saveData.HasCommittedCheckpoint;
        }
    }

    public bool HasValidSaveFile
    {
        get
        {
            if (!GameDataSaver.TryLoad(saveFileName, out GameSaveData loadedData))
                return false;

            return loadedData.HasCommittedCheckpoint;
        }
    }

    public bool ContinueRestorePending => continueRestorePending;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (dontDestroyOnLoad)
            DontDestroyOnLoad(gameObject);

        // Quan trọng:
        // Không LoadFromDisk() ở Awake.
        // New Game / Continue phải chủ động quyết định khi nào dùng save.
        EnsureRuntimeData();

        Debug.Log("[GameDataManager] Ready. Save path = " + SaveFilePath, this);
    }

    /// <summary>
    /// Tạo runtime data mới cho một New Game và xóa save cũ.
    /// Không tạo save.json ngay lập tức.
    /// File chỉ xuất hiện khi checkpoint đầu tiên được commit.
    /// </summary>
    public void StartNewGame()
    {
        saveData = CreateDefaultData();
        continueRestorePending = false;

        bool deleted = GameDataSaver.TryDelete(saveFileName);

        if (deleted)
            SaveCleared?.Invoke();

        Debug.Log(
            "[GameDataManager] New Game started. Save file cleared. " +
            "No save file will exist until the first committed checkpoint.",
            this
        );
    }

    /// <summary>
    /// Đọc save.json vào runtime memory.
    /// Không tự restore Scene / Player / Enemy.
    /// </summary>
    public bool LoadFromDisk()
    {
        if (!GameDataSaver.TryLoad(saveFileName, out GameSaveData loadedData))
        {
            Debug.LogWarning(
                "[GameDataManager] Save file was not found or could not be loaded: " +
                SaveFilePath,
                this
            );

            return false;
        }

        loadedData.EnsureLists();
        saveData = loadedData;

        Debug.Log(
            "[GameDataManager] Save loaded. " +
            "Map = " + saveData.CurrentMap +
            ", Checkpoint = " + saveData.CurrentCheckpoint +
            ", PlayerHealth = " + saveData.PlayerHealth,
            this
        );

        SaveLoaded?.Invoke(saveData);
        return true;
    }

    /// <summary>
    /// Ghi runtime data thành save.json.
    /// Chỉ dùng ở commit points hoặc các explicit persistent-save events.
    /// </summary>
    public bool SaveToDisk()
    {
        EnsureRuntimeData();

        bool success = GameDataSaver.TrySave(saveData, saveFileName);

        if (success)
            SaveWritten?.Invoke(saveData);

        return success;
    }

    /// <summary>
    /// Commit checkpoint chính.
    /// Phase hiện tại commit Map + Checkpoint.
    /// PlayerHealth / Mission / Enemy sẽ được bổ sung khi domain tương ứng nối vào.
    /// </summary>
    public bool CommitCheckpoint(int mapNumber, int checkpointNumber)
    {
        if (mapNumber < 1)
        {
            Debug.LogWarning("[GameDataManager] Invalid Map Number: " + mapNumber, this);
            return false;
        }

        if (checkpointNumber < 1)
        {
            Debug.LogWarning("[GameDataManager] Invalid Checkpoint Number: " + checkpointNumber, this);
            return false;
        }

        EnsureRuntimeData();

        saveData.CurrentMap = mapNumber;
        saveData.CurrentCheckpoint = checkpointNumber;

        return SaveToDisk();
    }

    public void SetPlayerHealth(int health)
    {
        EnsureRuntimeData();
        saveData.PlayerHealth = Mathf.Max(0, health);
    }

    public int GetPlayerHealthOrDefault(int defaultHealth = 100)
    {
        EnsureRuntimeData();

        return saveData.HasCommittedCheckpoint
            ? saveData.PlayerHealth
            : defaultHealth;
    }

    public bool AddCompletedMission(string missionId, bool saveImmediately = false)
    {
        EnsureRuntimeData();

        if (string.IsNullOrWhiteSpace(missionId))
            return false;

        saveData.EnsureLists();

        if (!saveData.CompletedMissionIds.Contains(missionId))
            saveData.CompletedMissionIds.Add(missionId);

        return !saveImmediately || SaveToDisk();
    }

    public bool AddCompletedObjective(string objectiveId, bool saveImmediately = false)
    {
        EnsureRuntimeData();

        if (string.IsNullOrWhiteSpace(objectiveId))
            return false;

        saveData.EnsureLists();

        if (!saveData.CompletedObjectiveIds.Contains(objectiveId))
            saveData.CompletedObjectiveIds.Add(objectiveId);

        return !saveImmediately || SaveToDisk();
    }

    public bool AddPermanentDeadEnemy(string enemyId, bool saveImmediately = false)
    {
        EnsureRuntimeData();

        if (string.IsNullOrWhiteSpace(enemyId))
            return false;

        saveData.EnsureLists();

        if (!saveData.PermanentDeadEnemyIds.Contains(enemyId))
            saveData.PermanentDeadEnemyIds.Add(enemyId);

        return !saveImmediately || SaveToDisk();
    }

    public bool IsMissionCompleted(string missionId)
    {
        EnsureRuntimeData();
        saveData.EnsureLists();

        return !string.IsNullOrWhiteSpace(missionId) &&
               saveData.CompletedMissionIds.Contains(missionId);
    }

    public bool IsObjectiveCompleted(string objectiveId)
    {
        EnsureRuntimeData();
        saveData.EnsureLists();

        return !string.IsNullOrWhiteSpace(objectiveId) &&
               saveData.CompletedObjectiveIds.Contains(objectiveId);
    }

    public bool IsEnemyPermanentlyDead(string enemyId)
    {
        EnsureRuntimeData();
        saveData.EnsureLists();

        return !string.IsNullOrWhiteSpace(enemyId) &&
               saveData.PermanentDeadEnemyIds.Contains(enemyId);
    }

    /// <summary>
    /// Continue flow gọi sau khi LoadFromDisk() thành công, trước khi LoadScene.
    /// GameSceneLoader vẫn là nơi quyết định scene nào được load.
    /// </summary>
    public void PrepareContinueRestore()
    {
        EnsureRuntimeData();

        if (!saveData.HasCommittedCheckpoint)
        {
            Debug.LogWarning(
                "[GameDataManager] Cannot prepare Continue restore because the loaded data has no valid checkpoint.",
                this
            );

            continueRestorePending = false;
            return;
        }

        continueRestorePending = true;
        ContinueRestorePrepared?.Invoke(saveData);

        Debug.Log(
            "[GameDataManager] Continue restore prepared for " +
            "M" + saveData.CurrentMap +
            " CP" + saveData.CurrentCheckpoint,
            this
        );
    }

    /// <summary>
    /// Scene loader / scene bootstrap gọi sau khi checkpoint đã được áp dụng.
    /// </summary>
    public void ConsumeContinueRestore()
    {
        if (!continueRestorePending)
            return;

        continueRestorePending = false;
        ContinueRestoreConsumed?.Invoke();

        Debug.Log("[GameDataManager] Continue restore consumed.", this);
    }

    public void DeleteSaveFile()
    {
        bool deleted = GameDataSaver.TryDelete(saveFileName);

        if (deleted)
            SaveCleared?.Invoke();
    }

    public List<string> GetPermanentDeadEnemyIdsCopy()
    {
        EnsureRuntimeData();
        saveData.EnsureLists();

        return new List<string>(saveData.PermanentDeadEnemyIds);
    }

    public List<string> GetCompletedMissionIdsCopy()
    {
        EnsureRuntimeData();
        saveData.EnsureLists();

        return new List<string>(saveData.CompletedMissionIds);
    }

    public List<string> GetCompletedObjectiveIdsCopy()
    {
        EnsureRuntimeData();
        saveData.EnsureLists();

        return new List<string>(saveData.CompletedObjectiveIds);
    }

    public void EnsureRuntimeData()
    {
        if (saveData == null)
            saveData = CreateDefaultData();

        saveData.EnsureLists();
    }

    private GameSaveData CreateDefaultData()
    {
        return new GameSaveData
        {
            SaveVersion = 2,
            CurrentMap = 0,
            CurrentCheckpoint = 0,
            PlayerHealth = 100,
            CompletedMissionIds = new List<string>(),
            CompletedObjectiveIds = new List<string>(),
            PermanentDeadEnemyIds = new List<string>()
        };
    }
}
