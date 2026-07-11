// File: QuestInfoSO.cs
using UnityEngine;

[CreateAssetMenu(fileName = "New Quest", menuName = "DATN/Quest System/Quest Info", order = 0)]
public class QuestInfoSO : ScriptableObject
{
    [field: SerializeField] public string Id { get; private set; }
    
    [Header("General Info")]
    public string displayName;
    [TextArea] public string description;

    [Header("Objectives")]
    // Vì là cốt truyện tuyến tính, mỗi quest có thể có 1 hoặc nhiều bước (objective) liên tiếp nhau
    public string[] objectiveDescriptions;
    public int[] requiredAmounts;

    [Header("Rewards & Flow")]
    public int intelPointsReward;
    public int weponTokenReward;
    public GameObject[] itemRewards; // Có thể là Prefab của Item hoặc ScriptableObject của Item
    public int experienceReward;
    public QuestInfoSO nextLinearQuest; // Trỏ tới nhiệm vụ tiếp theo trong chuỗi

    private void OnValidate()
    {
        // Tự động sinh ID duy nhất cho Quest dựa trên tên file để tránh trùng lặp
        if (string.IsNullOrEmpty(Id))
        {
            Id = this.name;
        }
    }
}