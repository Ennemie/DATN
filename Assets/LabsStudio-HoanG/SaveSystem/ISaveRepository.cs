using System;

public interface ISaveRepository
{
    // Bản hợp đồng bắt buộc các kho (Local/Firebase) phải có hàm Save
    void Save(GameSaveDTO data);

    // Bản hợp đồng bắt buộc các kho (Local/Firebase) phải có hàm Load
    void Load(Action<GameSaveDTO> onLoaded);
}