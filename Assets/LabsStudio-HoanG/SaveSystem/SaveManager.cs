using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;

public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    // BỘ NÃO TRONG RAM: Giữ trạng thái dữ liệu sống của người chơi
    public GameSaveDTO CurrentState { get; private set; }

    // DANH SÁCH ĐĂNG KÝ: Giữ danh sách các hệ thống đang chạy muốn lưu game
    private readonly HashSet<ISaveable> saveableObjects = new HashSet<ISaveable>();

    private ISaveRepository localRepository;
    private ISaveRepository cloudRepository;

    void Awake()
    {
        // Cơ chế Singleton: Đảm bảo chỉ có duy nhất 1 SaveManager tồn tại và bất tử qua các Scene
        if (Instance == null) 
        { 
            Instance = this; 
            DontDestroyOnLoad(gameObject); 
            Debug.Log("<color=cyan>[SaveManager]</color> Singleton khởi tạo thành công. DontDestroyOnLoad đã kích hoạt.");
        }
        else 
        { 
            Debug.LogWarning("<color=yellow>[SaveManager]</color> Phát hiện bản trùng lặp → Destroy(gameObject)");
            Destroy(gameObject); 
            return;
        }

        CurrentState = new GameSaveDTO();
        localRepository = new LocalSaveRepository();
        cloudRepository = new FirebaseSaveRepository();
        Debug.Log("<color=cyan>[SaveManager]</color> Awake() hoàn tất. " +
                  "CurrentState khởi tạo mới | LocalRepository ✓ | CloudRepository ✓");
    }

    void Update()
    {
        // PHÍM TẮT ĐỂ TEST CLOUD SAVE/LOAD KHI GAME ĐANG CHẠY
        if (Input.GetKeyDown(KeyCode.F5))
        {
            Debug.Log("<color=white>[TEST]</color> Người chơi bấm F5 → Kích hoạt Save Game");
            TriggerSaveGame();
        }
        if (Input.GetKeyDown(KeyCode.F9))
        {
            Debug.Log("<color=white>[TEST]</color> Người chơi bấm F9 → Kích hoạt Load Game");
            TriggerLoadGame();
        }
    }

    // --- TỰ ĐỘNG SAVE KHI TẮT GAME / TẮT PLAY MODE (Chỉ dùng cho Local) ---
    void OnApplicationQuit()
    {
        Debug.Log("<color=yellow>[SaveManager]</color> OnApplicationQuit → Tự động Save Local trước khi thoát...");

        // Thu thập dữ liệu từ tất cả hệ thống đã đăng ký
        foreach (var saveable in saveableObjects)
        {
            saveable.CaptureState(CurrentState);
        }

        // Ghi file ĐỒNG BỘ (không dùng async vì app đang tắt, Task.Run sẽ không kịp hoàn thành)
        localRepository.Save(CurrentState);

        Debug.Log("<color=lime>[SaveManager]</color> ■ Auto-Save trước khi thoát HOÀN TẤT");
    }

    // --- CƠ CHẾ ĐĂNG KÝ TỰ ĐỘNG (REGISTRY PATTERN) ---
    public void RegisterSaveable(ISaveable saveable)
    {
        saveableObjects.Add(saveable);
        Debug.Log($"<color=cyan>[SaveManager]</color> + Đăng ký: {saveable.GetType().Name} | " +
                  $"Tổng hệ thống đã đăng ký: {saveableObjects.Count}");
    }

    public void UnregisterSaveable(ISaveable saveable)
    {
        saveableObjects.Remove(saveable);
        Debug.Log($"<color=gray>[SaveManager]</color> - Hủy đăng ký: {saveable.GetType().Name} | " +
                  $"Tổng hệ thống còn lại: {saveableObjects.Count}");
    }

    // ========================================================
    // LƯU GAME BẤT ĐỒNG BỘ (ASYNC SAVE)
    // ========================================================
    public async void TriggerSaveGame()
    {
        Debug.Log($"<color=yellow>[SaveManager]</color> ════════════ SAVE BẮT ĐẦU ════════════ " +
                  $"Số hệ thống cần thu thập: {saveableObjects.Count}");
        
        // 1. Bắt tất cả các ông đã đăng ký tự đổ dữ liệu của mình vào CurrentState trong RAM
        foreach (var saveable in saveableObjects)
        {
            Debug.Log($"<color=yellow>[SaveManager]</color>   ├─ Thu thập từ: {saveable.GetType().Name}...");
            saveable.CaptureState(CurrentState);
        }

        Debug.Log("<color=yellow>[SaveManager]</color>   ├─ Thu thập RAM hoàn tất. Đang ghi file vật lý (Thread ngầm)...");

        // 2. Đẩy việc ghi file vật lý sang Thread ngầm để không gây lag khung hình Game (FPS)
        // Thư viện Newtonsoft.Json và File I/O sẽ chạy ngầm ở đây
        await Task.Run(() => localRepository.Save(CurrentState));

        // 3. Đẩy dữ liệu lên Cloud SQL Connect (Hàm này nội bộ Firebase đã chạy ngầm)
        cloudRepository.Save(CurrentState);

        Debug.Log("<color=lime>[SaveManager]</color> ════════════ SAVE HOÀN TẤT ════════════");
    }

    // ========================================================
    // LOAD GAME (RESTORE STATE)
    // ========================================================
    public void TriggerLoadGame()
    {
        Debug.Log($"<color=cyan>[SaveManager]</color> ════════════ LOAD BẮT ĐẦU ════════════ " +
                  $"Số hệ thống cần khôi phục: {saveableObjects.Count}");

        // Ưu tiên gọi Cloud Load, cơ chế Fallback của Repository sẽ tự xử lý nếu mất mạng
        cloudRepository.Load(loadedData =>
        {
            if (loadedData != null)
            {
                // Cập nhật dữ liệu từ nguồn lưu trữ vào Bộ não RAM
                CurrentState = loadedData;
                Debug.Log("<color=cyan>[SaveManager]</color>   ├─ Dữ liệu Cloud nạp vào RAM thành công.");

                // Phát lệnh cho tất cả các hệ thống đã đăng ký tự bốc phần dữ liệu của mình ra xài
                foreach (var saveable in saveableObjects)
                {
                    Debug.Log($"<color=cyan>[SaveManager]</color>   ├─ Khôi phục cho: {saveable.GetType().Name}...");
                    saveable.RestoreState(CurrentState);
                }
                Debug.Log("<color=lime>[SaveManager]</color> ════════════ LOAD HOÀN TẤT ════════════");
            }
            else
            {
                Debug.LogWarning("<color=yellow>[SaveManager]</color>   ├─ Cloud trả về null → Kích hoạt Fallback Local...");

                // FALLBACK: Cloud thất bại, chuyển sang đọc file Local
                localRepository.Load(localData =>
                {
                    if (localData != null)
                    {
                        CurrentState = localData;
                        Debug.Log("<color=cyan>[SaveManager]</color>   ├─ Dữ liệu Local nạp vào RAM thành công.");

                        foreach (var saveable in saveableObjects)
                        {
                            Debug.Log($"<color=cyan>[SaveManager]</color>   ├─ Khôi phục cho: {saveable.GetType().Name}...");
                            saveable.RestoreState(CurrentState);
                        }
                        Debug.Log("<color=lime>[SaveManager]</color> ════════════ LOAD HOÀN TẤT (Local Fallback) ════════════");
                    }
                    else
                    {
                        Debug.LogWarning("<color=yellow>[SaveManager]</color>   └─ Không có dữ liệu nào. Giữ nguyên CurrentState mặc định (game mới).");
                    }
                });
            }
        });
    }
}