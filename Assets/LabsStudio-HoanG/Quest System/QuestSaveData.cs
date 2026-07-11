[System.Serializable]
public class QuestSaveData
{
    public string questId;
    public QuestState state;
    public int currentObjectiveIndex;
    public int currentProgress;

    public QuestSaveData(string id, QuestState initialState)
    {
        questId = id;
        state = initialState;
        currentObjectiveIndex = 0;
        currentProgress = 0;
    }
}