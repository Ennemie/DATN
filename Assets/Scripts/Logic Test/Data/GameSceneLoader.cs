// Chức năng: Persistent bootstrap cho Menu -> Tutorial / Villa Continue.
// GameSceneLoader được đặt ở Menu và DontDestroyOnLoad.
// - New Game: clear save rồi load Tutorial bình thường.
// - Continue: đọc save.json ở Menu, xác định scene theo CurrentMap,
//   chuẩn bị restore, load scene, rồi ApplySavedCheckpoint sau khi scene load.
// - Load scene bình thường / PlayMode trực tiếp không tự restore.
// - Không cần GameDataManager ở Tutorial hay Villa nếu instance từ Menu còn tồn tại.

using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameSceneLoader : MonoBehaviour
{
    [Serializable]
    public class MapSceneEntry
    {
        [Min(1)]
        public int mapNumber = 1;

        [Tooltip("Tên Scene chính xác như trong Build Settings.")]
        public string sceneName;
    }

    [Header("Game Data")]
    [SerializeField] private GameDataManager dataManager;
    [SerializeField] private bool dontDestroyOnLoad = true;

    [Header("Scene Setup")]
    [SerializeField] private string newGameSceneName = "Tutorial";
    [SerializeField] private MapSceneEntry[] mapScenes;

    private bool continueRestorePending;
    private GameSaveData continueData;
    private bool isDestroying;

    public event Action<GameSaveData> ContinueSceneReady;

    public bool CanContinue
    {
        get
        {
            return dataManager != null && dataManager.HasValidSaveFile;
        }
    }

    private void Awake()
    {
        if (dontDestroyOnLoad)
        {
            GameSceneLoader existing = FindAnyObjectByType<GameSceneLoader>();

            if (existing != null && existing != this)
            {
                Destroy(gameObject);
                return;
            }

            DontDestroyOnLoad(gameObject);
        }

        EnsureDataManager();

        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDestroy()
    {
        isDestroying = true;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    /// <summary>
    /// UI Button: New Game.
    /// Không restore save cũ.
    /// </summary>
    public void StartNewGame()
    {
        EnsureDataManager();

        if (dataManager == null)
        {
            Debug.LogError(
                "[GameSceneLoader] GameDataManager is missing in Menu. New Game cannot start.",
                this
            );
            return;
        }

        continueRestorePending = false;
        continueData = null;

        dataManager.StartNewGame();

        if (string.IsNullOrWhiteSpace(newGameSceneName))
        {
            Debug.LogError("[GameSceneLoader] New Game Scene Name is empty.", this);
            return;
        }

        Debug.Log(
            "[GameSceneLoader] New Game -> loading scene '" +
            newGameSceneName + "'.",
            this
        );

        SceneManager.LoadScene(newGameSceneName);
    }

    /// <summary>
    /// UI Button: Continue.
    /// Chỉ flow này mới nạp save.json và kích hoạt restore bootstrap.
    /// </summary>
    public void ContinueGame()
    {
        EnsureDataManager();

        if (dataManager == null)
        {
            Debug.LogError(
                "[GameSceneLoader] GameDataManager is missing in Menu. Continue cannot start.",
                this
            );
            return;
        }

        if (!dataManager.HasSaveFile)
        {
            Debug.Log(
                "[GameSceneLoader] Continue ignored because no save file exists.\nPath = " +
                dataManager.SaveFilePath,
                this
            );
            return;
        }

        if (!dataManager.LoadFromDisk())
        {
            Debug.LogWarning(
                "[GameSceneLoader] Continue aborted because save data could not be loaded.",
                this
            );
            return;
        }

        GameSaveData loadedData = dataManager.Data;

        if (!loadedData.HasCommittedCheckpoint)
        {
            Debug.LogWarning(
                "[GameSceneLoader] Continue aborted because save has no valid checkpoint. " +
                "Map = " + loadedData.CurrentMap +
                ", Checkpoint = " + loadedData.CurrentCheckpoint,
                this
            );
            return;
        }

        string sceneName = ResolveSceneName(loadedData.CurrentMap);

        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError(
                "[GameSceneLoader] No scene mapping was found for Map Number " +
                loadedData.CurrentMap,
                this
            );
            return;
        }

        continueData = loadedData.Clone();
        continueRestorePending = true;
        dataManager.PrepareContinueRestore();

        Debug.Log(
            "[GameSceneLoader] Continue -> loading scene '" +
            sceneName +
            "' for M" + loadedData.CurrentMap +
            " CP" + loadedData.CurrentCheckpoint +
            ".",
            this
        );

        SceneManager.LoadScene(sceneName);
    }

    // Cũ:
    // private void HandleSceneLoaded(Scene scene, LoadSceneMode loadMode)
    // {
    //     if (isDestroying)
    //         return;
    //
    //     if (!continueRestorePending || continueData == null)
    //         return;
    //
    //     CheckpointManager checkpointManager = FindAnyObjectByType<CheckpointManager>();
    //
    //     if (checkpointManager == null)
    //     {
    //         Debug.LogError(
    //             "[GameSceneLoader] Continue scene loaded, but no CheckpointManager was found.",
    //             this
    //         );
    //         continueRestorePending = false;
    //         continueData = null;
    //         dataManager?.ConsumeContinueRestore();
    //         return;
    //     }
    //
    //     bool applied = checkpointManager.ApplySavedCheckpoint(continueData);
    //
    //     if (!applied)
    //     {
    //         Debug.LogError(
    //             "[GameSceneLoader] Continue restore failed in scene '" +
    //             scene.name +
    //             "' for M" + continueData.CurrentMap +
    //             " CP" + continueData.CurrentCheckpoint,
    //             this
    //         );
    //         continueRestorePending = false;
    //         continueData = null;
    //         dataManager?.ConsumeContinueRestore();
    //         return;
    //     }
    //
    //     dataManager?.ConsumeContinueRestore();
    //
    //     ContinueSceneReady?.Invoke(continueData);
    //
    //     Debug.Log(
    //         "[GameSceneLoader] Continue restore bootstrap completed in scene '" +
    //         scene.name +
    //         "'.",
    //         this
    //     );
    //
    //     continueRestorePending = false;
    //     continueData = null;
    // }
    //
    // MỚI:
    // Delay restore để các object trong scene hoàn tất Awake/OnEnable/Start và các coroutine
    // khởi tạo một frame trước khi CheckpointManager phát event restore.
    private void HandleSceneLoaded(Scene scene, LoadSceneMode loadMode)
    {
        if (isDestroying)
            return;

        if (!continueRestorePending || continueData == null)
            return;

        StartCoroutine(ApplyContinueRestoreAfterSceneInitialization(scene));
    }

    private System.Collections.IEnumerator ApplyContinueRestoreAfterSceneInitialization(Scene scene)
    {
        // Chức năng mới:
        // Chờ 2 frame thay vì 1 để các Start coroutine một-frame của EnemyCheckpointHandler
        // hoàn tất việc chụp baseline HP/AI trước khi CheckpointManager phát event restore.
        // CŨ:
        // yield return null;
        //
        // MỚI:
        yield return null;
        yield return null;

        if (isDestroying || !continueRestorePending || continueData == null)
            yield break;

        CheckpointManager checkpointManager = FindAnyObjectByType<CheckpointManager>();

        if (checkpointManager == null)
        {
            Debug.LogError(
                "[GameSceneLoader] Continue scene loaded, but no CheckpointManager was found.",
                this
            );
            continueRestorePending = false;
            continueData = null;
            dataManager?.ConsumeContinueRestore();
            yield break;
        }

        bool applied = checkpointManager.ApplySavedCheckpoint(continueData);

        if (!applied)
        {
            Debug.LogError(
                "[GameSceneLoader] Continue restore failed in scene '" +
                scene.name +
                "' for M" + continueData.CurrentMap +
                " CP" + continueData.CurrentCheckpoint,
                this
            );
            continueRestorePending = false;
            continueData = null;
            dataManager?.ConsumeContinueRestore();
            yield break;
        }

        GameSaveData restoredData = continueData;

        dataManager?.ConsumeContinueRestore();
        ContinueSceneReady?.Invoke(restoredData);

        Debug.Log(
            "[GameSceneLoader] Continue restore bootstrap completed in scene '" +
            scene.name +
            "'.",
            this
        );

        continueRestorePending = false;
        continueData = null;
    }

    private string ResolveSceneName(int mapNumber)
    {
        if (mapScenes == null)
            return null;

        for (int i = 0; i < mapScenes.Length; i++)
        {
            MapSceneEntry entry = mapScenes[i];

            if (entry == null)
                continue;

            if (entry.mapNumber != mapNumber)
                continue;

            return entry.sceneName;
        }

        return null;
    }

    private void EnsureDataManager()
    {
        if (dataManager != null)
            return;

        dataManager = GameDataManager.Instance;

        if (dataManager == null)
            dataManager = FindAnyObjectByType<GameDataManager>();
    }
}
