// Chức năng: Low-level JSON file I/O cho Save System.
// Đây KHÔNG phải MonoBehaviour.
// Không biết gì về Scene, Player, Enemy hay Checkpoint gameplay.

using System;
using System.IO;
using UnityEngine;

public static class GameDataSaver
{
    public static string GetSaveFilePath(string fileName = "save.json")
    {
        string safeFileName = string.IsNullOrWhiteSpace(fileName)
            ? "save.json"
            : fileName.Trim();

        return Path.Combine(Application.persistentDataPath, safeFileName);
    }

    public static bool Exists(string fileName = "save.json")
    {
        return File.Exists(GetSaveFilePath(fileName));
    }

    public static bool TrySave(GameSaveData data, string fileName = "save.json")
    {
        if (data == null)
        {
            Debug.LogError("[GameDataSaver] Cannot save because data is null.");
            return false;
        }

        try
        {
            data.EnsureLists();

            string path = GetSaveFilePath(fileName);
            string directory = Path.GetDirectoryName(path);

            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(path, json);

            Debug.Log("[GameDataSaver] Save written: " + path);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError("[GameDataSaver] Failed to write save file.\n" + exception);
            return false;
        }
    }

    public static bool TryLoad(string fileName, out GameSaveData data)
    {
        data = null;

        string path = GetSaveFilePath(fileName);

        if (!File.Exists(path))
            return false;

        try
        {
            string json = File.ReadAllText(path);
            GameSaveData loadedData = JsonUtility.FromJson<GameSaveData>(json);

            if (loadedData == null)
            {
                Debug.LogError("[GameDataSaver] Save JSON could not be deserialized: " + path);
                return false;
            }

            loadedData.EnsureLists();
            data = loadedData;
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "[GameDataSaver] Failed to read save file.\n" +
                "Path = " + path + "\n" +
                exception
            );

            return false;
        }
    }

    public static bool TryDelete(string fileName = "save.json")
    {
        string path = GetSaveFilePath(fileName);

        try
        {
            if (File.Exists(path))
                File.Delete(path);

            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "[GameDataSaver] Failed to delete save file.\n" +
                "Path = " + path + "\n" +
                exception
            );

            return false;
        }
    }
}
