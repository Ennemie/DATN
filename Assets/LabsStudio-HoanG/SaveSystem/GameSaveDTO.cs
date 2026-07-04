using System;
using System.Collections.Generic;

// ========================================================
// TỔNG KHO CHỨA DỮ LIỆU RAM (Khớp 100% với cấu trúc Database)
// ========================================================

[System.Serializable]
public class GameSaveDTO
{
    // --- Gốc từ bảng Player ---
    public string username;
    public int dataVersion = 1;
    public long lastUpdated; // Dùng kiểu long để lưu Unix Timestamp cho nhẹ và chuẩn xác

    // --- Các khối dữ liệu quan hệ (Bảng con) ---
    public ResourcesData resources = new ResourcesData();
    public ProgressData progress = new ProgressData();
    public List<MissionData> missions = new List<MissionData>();
    public List<GadgetData> gadgets = new List<GadgetData>();
}

// ========================================================
// CÁC THÀNH PHẦN CON ĐƯỢC PHÂN TÁCH THEO BẢNG
// ========================================================

// Khối dữ liệu tương ứng với bảng [Resources]
[System.Serializable]
public class ResourcesData
{
    public int intelPoints;
    public int weaponTokens;
}

// Khối dữ liệu tương ứng với bảng [Progress]
[System.Serializable]
public class ProgressData
{
    public string currentChapter;
}

// Cấu trúc một phần tử trong danh sách của bảng [Mission]
[System.Serializable]
public class MissionData
{
    public string missionId;
    public bool mainClear;
    public bool stealthBonus;
    public bool allIntelFound;
}

// Cấu trúc một phần tử trong danh sách của bảng [Gadget]
[System.Serializable]
public class GadgetData
{
    public string gadgetId;
    public int currentLevel;
    public bool isUnlocked;
}