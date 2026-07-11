using UnityEngine;

public class Quest
{
    public QuestInfoSO Info { get; private set; }
    public QuestSaveData SaveData { get; private set; }

    public Quest(QuestInfoSO questInfo, QuestSaveData saveData)
    {
        Info = questInfo;
        SaveData = saveData;
    }

    // Các hàm xử lý logic nội bộ
    public void AddProgress(int amount)
    {
        if (SaveData.state != QuestState.Active) return;

        SaveData.currentProgress += amount;
        
        int currentRequired = Info.requiredAmounts[SaveData.currentObjectiveIndex];
        
        if (SaveData.currentProgress >= currentRequired)
        {
            CompleteCurrentObjective();
        }
    }

    private void CompleteCurrentObjective()
    {
        SaveData.currentObjectiveIndex++;
        SaveData.currentProgress = 0;

        // Nếu đã làm hết các bước trong quest này
        if (SaveData.currentObjectiveIndex >= Info.objectiveDescriptions.Length)
        {
            SaveData.state = QuestState.Completed;
        }
    }
}