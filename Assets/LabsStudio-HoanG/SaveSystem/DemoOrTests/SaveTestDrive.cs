using UnityEngine;
using Firebase.Auth;

public class SaveTestDrive : MonoBehaviour
{
    private ISaveRepository saveRepository;

    void Start()
    {
        // Nếu ông đã chạy Scene Đăng nhập trước đó và đăng nhập thành công
        if (FirebaseAuth.DefaultInstance.CurrentUser != null)
        {
            Debug.Log("<color=green>Auth Success:</color> Đã nhận diện được điệp viên đăng nhập!");
            
            // CHẠY LUÔN TEST SAVE/LOAD CLOUD
            RunCloudSaveLoadTest();
        }
        else
        {
            Debug.LogError("FirebaseSave: Ông phải mở Scene Đăng Nhập để login tài khoản của ông trước đã thì mới có UID để test Cloud!");
        }
    }

    private void RunCloudSaveLoadTest()
    {
        // Khởi tạo 2 kho lưu trữ song song
        ISaveRepository cloudRepo = new FirebaseSaveRepository();
        ISaveRepository localRepo = new LocalSaveRepository();

        // 1. KHỞI TẠO DỮ LIỆU ĐẦY ĐỦ 5 BẢNG (Khớp 100% với image_393509.png)
        GameSaveDTO myCurrentGame = new GameSaveDTO();

        // Bảng Player & Bảng Resources
        myCurrentGame.Resources.IntelPoints = 7777;
        myCurrentGame.Resources.WeaponTokens = 5;

        // Bảng Progress
        myCurrentGame.Progression.CurrentChapter = "chapter_02_casino";

        // Bảng Mission (Lưu danh sách các màn chơi đã hoàn thành)
        myCurrentGame.Progression.CompletedMissions.Clear();
        myCurrentGame.Progression.CompletedMissions.Add(new MissionState { 
            MissionId = "mission_01_tutorial", 
            MainClear = true, 
            StealthBonus = true, 
            AllIntelFound = true 
        });
        myCurrentGame.Progression.CompletedMissions.Add(new MissionState { 
            MissionId = "mission_02_infiltration", 
            MainClear = true, 
            StealthBonus = false, 
            AllIntelFound = true 
        });

        // Bảng Gadget (Lưu kho đồ chơi công nghệ của điệp viên)
        myCurrentGame.Inventory.Gadgets.Clear();
        myCurrentGame.Inventory.Gadgets.Add(new GadgetState { GadgetId = "laser_watch", Level = 3, IsUnlocked = true });
        myCurrentGame.Inventory.Gadgets.Add(new GadgetState { GadgetId = "emp_grenade", Level = 1, IsUnlocked = true });
        myCurrentGame.Inventory.Gadgets.Add(new GadgetState { GadgetId = "grappling_hook", Level = 0, IsUnlocked = false });

        // 2. TIẾN HÀNH LƯU ĐỒNG THỜI (Giờ dữ liệu đẩy lên sẽ full luôn các bảng)
        localRepo.Save(myCurrentGame);
        cloudRepo.Save(myCurrentGame);

        // 3. GIẢ LẬP LOAD ĐỂ KIỂM TRA LOG
        cloudRepo.Load(gameDataFromCloud => 
        {
            if (gameDataFromCloud != null)
            {
                Debug.Log("<color=green>LOAD CLOUD THÀNH CÔNG!</color>");
                Debug.Log($"[Resources] Intel: {gameDataFromCloud.Resources.IntelPoints}, Tokens: {gameDataFromCloud.Resources.WeaponTokens}");
                Debug.Log($"[Progress] Chapter hiện tại: {gameDataFromCloud.Progression.CurrentChapter}");
                Debug.Log($"[Missions] Đã đi qua {gameDataFromCloud.Progression.CompletedMissions.Count} màn chơi.");
                Debug.Log($"[Gadgets] Kho đồ công nghệ có {gameDataFromCloud.Inventory.Gadgets.Count} món.");
            }
        });
    }
}