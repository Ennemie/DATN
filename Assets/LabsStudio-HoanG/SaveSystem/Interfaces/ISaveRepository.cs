using System;

public interface ISaveRepository
{
    // Hàm nhận vào một cục DTO và tiến hành lưu trữ
    void Save(GameSaveDTO data);

    // Hàm đọc dữ liệu lên và trả về một cục DTO
    void Load(Action<GameSaveDTO> onLoaded);
}