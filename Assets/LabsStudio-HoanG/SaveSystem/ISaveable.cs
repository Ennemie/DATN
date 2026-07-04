// GIAO DIỆN BẮT BUỘC CHO CÁC HỆ THỐNG MUỐN LƯU GAME
public interface ISaveable
{
    // Lệnh thu thập: "Đổ dữ liệu từ RAM của ông vào cục Save chung đi"
    void CaptureState(GameSaveDTO currentSave);

    // Lệnh khôi phục: "Dữ liệu từ Cloud/Local về rồi, nhặt phần của ông ra nạp lại đi"
    void RestoreState(GameSaveDTO currentSave);
}