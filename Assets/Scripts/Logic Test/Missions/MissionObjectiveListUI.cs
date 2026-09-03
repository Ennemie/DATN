// Chức năng: Quản lý danh sách nhiệm vụ active ở góc trái màn hình.
// - Nhận objective mới sau khi popup New Objective biến mất.
// - Instantiate clone từ một RootObject/template đang inactive trong hierarchy hoặc prefab asset.
// - Bắt buộc SetActive(true) cho clone sau khi Instantiate để clone hiển thị dù template đang inactive.
// - Clone đầu tiên lấy đúng anchoredPosition của template/root mẫu; các clone sau cách nhau theo trục Y.
// - Khi objective complete: đổi item sang trạng thái xanh, delay, slide out sang trái, Destroy clone, rồi dồn các item bên dưới lên.
// Gán cho: GameManager hoặc một object UI/Manager luôn active.
// Tham chiếu với: MissionObjectiveUI gọi AddObjective() sau popup; MissionObjective gọi CompleteObjective() khi nhiệm vụ hoàn tất; MissionObjectiveListItemUI là script trên RootObject/template của từng dòng nhiệm vụ.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MissionObjectiveListUI : MonoBehaviour
{
    public static MissionObjectiveListUI Instance { get; private set; }

    [Header("References")]
    [Tooltip("Parent/container chứa các clone objective. Nếu để trống, script sẽ dùng parent của Objective Item Prefab/Template.")]
    [SerializeField] private RectTransform objectiveItemRoot;

    [Tooltip("RootObject/template của 1 dòng objective. Có thể là prefab asset hoặc một object inactive đang nằm sẵn trong Canvas hierarchy.")]
    [SerializeField] private MissionObjectiveListItemUI objectiveItemPrefab;

    [Header("Template Options")]
    [Tooltip("Bật true nếu Objective Item Prefab là object mẫu inactive trong hierarchy. Script sẽ giữ template inactive và chỉ bật clone.")]
    [SerializeField] private bool keepTemplateInactive = true;

    [Tooltip("Bật true để clone đầu tiên lấy đúng anchoredPosition của Objective Item Prefab/Template.")]
    [SerializeField] private bool useTemplatePositionAsFirstPosition = true;

    [Header("Layout")]
    [Tooltip("Chỉ dùng khi Use Template Position As First Position = false hoặc template không có RectTransform.")]
    [SerializeField] private Vector2 firstItemAnchoredPosition = Vector2.zero;

    [Tooltip("Khoảng cách Y giữa 2 objective. Dùng số dương; script tự trừ xuống dưới.")]
    [SerializeField] private float itemSpacingY = 35f;

    [SerializeField] private float layoutMoveDuration = 0.25f;

    [Header("Complete Animation")]
    [SerializeField] private float completeDelay = 1f;
    [SerializeField] private float slideOutDuration = 0.25f;
    [SerializeField] private float slideOutLeftDistance = 500f;

    [Header("Colors")]
    [SerializeField] private Color activeTextColor = Color.yellow;
    [SerializeField] private Color completedTextColor = Color.green;

    [Header("Options")]
    [SerializeField] private bool ignoreDuplicateObjectiveId = true;

    [Tooltip(
        "Nếu bật, Objective Completed có trong save cũng được dựng lại trên Canvas. " +
        "Mặc định false để giữ behavior cũ: Completed item hoàn tất animation rồi rời active list."
    )]
    [SerializeField] private bool showCompletedObjectivesOnRestore = false;

    [SerializeField] private bool logDebug;

    private readonly List<MissionObjectiveListItemUI> activeItems = new List<MissionObjectiveListItemUI>();
    private readonly Dictionary<string, MissionObjectiveListItemUI> itemById = new Dictionary<string, MissionObjectiveListItemUI>();
    private readonly HashSet<string> completingIds = new HashSet<string>();

    private Vector2 cachedTemplateAnchoredPosition;
    private bool hasCachedTemplatePosition;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Debug.LogWarning("[MissionObjectiveListUI] More than one instance found. Destroying duplicate on " + name);
            Destroy(gameObject);
            return;
        }

        CacheTemplateSetup();
    }

    private void CacheTemplateSetup()
    {
        if (objectiveItemPrefab == null)
        {
            Debug.LogWarning("[MissionObjectiveListUI] Objective Item Prefab/Template is missing.");
            return;
        }

        RectTransform templateRect = objectiveItemPrefab.RectTransform;
        if (templateRect != null)
        {
            cachedTemplateAnchoredPosition = templateRect.anchoredPosition;
            hasCachedTemplatePosition = true;

            if (objectiveItemRoot == null)
                objectiveItemRoot = templateRect.parent as RectTransform;
        }

        if (keepTemplateInactive && objectiveItemPrefab.gameObject.scene.IsValid())
        {
            objectiveItemPrefab.gameObject.SetActive(false);
        }
    }

    public void AddObjective(string objectiveId, string objectiveText)
    {
        string finalId = NormalizeId(objectiveId, objectiveText);

        if (string.IsNullOrWhiteSpace(objectiveText))
        {
            Debug.LogWarning("[MissionObjectiveListUI] AddObjective ignored because objectiveText is empty.");
            return;
        }

        if (itemById.ContainsKey(finalId))
        {
            if (ignoreDuplicateObjectiveId)
            {
                if (logDebug)
                    Debug.Log("[MissionObjectiveListUI] Objective already exists: " + finalId);

                return;
            }

            finalId = finalId + "_" + Time.frameCount;
        }

        if (objectiveItemPrefab == null)
        {
            Debug.LogError("[MissionObjectiveListUI] Missing Objective Item Prefab/Template.");
            return;
        }

        RectTransform parent = objectiveItemRoot;
        if (parent == null && objectiveItemPrefab.RectTransform != null)
            parent = objectiveItemPrefab.RectTransform.parent as RectTransform;

        if (parent == null)
        {
            Debug.LogError("[MissionObjectiveListUI] Missing Objective Item Root/Parent. Assign Objective Item Root or put the template under a RectTransform parent.");
            return;
        }

        MissionObjectiveListItemUI item = Instantiate(objectiveItemPrefab, parent, false);
        item.name = objectiveItemPrefab.name + "_Clone_" + finalId;

        // Rất quan trọng: nếu template/root object đang inactive thì clone cũng sinh ra inactive.
        // Bắt buộc bật clone lên trước khi Initialize để UI con hiển thị đúng.
        item.gameObject.SetActive(true);

        Vector2 targetPosition = GetPositionForIndex(activeItems.Count);
        if (item.RectTransform != null)
            item.RectTransform.anchoredPosition = targetPosition;

        item.Initialize(finalId, objectiveText, activeTextColor);

        activeItems.Add(item);
        itemById[finalId] = item;
        RepositionItems(true);

        if (keepTemplateInactive && objectiveItemPrefab.gameObject.scene.IsValid())
            objectiveItemPrefab.gameObject.SetActive(false);

        if (logDebug)
            Debug.Log("[MissionObjectiveListUI] Added objective clone: " + finalId + " / " + objectiveText);
    }

    /// <summary>
    /// Rebuild Canvas Objective List từ persistent Mission state.
    ///
    /// Không chạy popup New Objective.
    /// Không dùng animation completion.
    /// </summary>
    public void RestoreFromPersistentState(
        System.Collections.Generic.IList<MissionObjectiveSaveData> states)
    {
        ClearAllObjectives();

        if (states == null)
            return;

        for (int i = 0; i < states.Count; i++)
        {
            MissionObjectiveSaveData state = states[i];

            if (state == null ||
                string.IsNullOrWhiteSpace(state.ObjectiveId))
            {
                continue;
            }

            if (state.State == MissionProgressState.Started)
            {
                if (!state.ShowInActiveList)
                    continue;

                AddObjective(
                    state.ObjectiveId,
                    ResolveRestoreText(state)
                );

                continue;
            }

            if (state.State ==
                    MissionProgressState.Completed &&
                state.ShowInActiveList &&
                showCompletedObjectivesOnRestore)
            {
                AddObjective(
                    state.ObjectiveId,
                    ResolveRestoreText(state)
                );

                if (itemById.TryGetValue(
                        state.ObjectiveId,
                        out MissionObjectiveListItemUI item))
                {
                    item.MarkCompleted(
                        completedTextColor
                    );
                }
            }
        }

        RepositionItems(false);

        if (logDebug)
        {
            Debug.Log(
                "[MissionObjectiveListUI] Restored " +
                activeItems.Count +
                " objective UI item(s) from persistent state."
            );
        }
    }

    private string ResolveRestoreText(
        MissionObjectiveSaveData state)
    {
        if (state == null)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(
                state.DisplayText))
        {
            return state.DisplayText;
        }

        return state.ObjectiveId;
    }

    public void CompleteObjective(string objectiveId)
    {
        if (string.IsNullOrWhiteSpace(objectiveId))
        {
            Debug.LogWarning("[MissionObjectiveListUI] CompleteObjective ignored because objectiveId is empty.");
            return;
        }

        string finalId = objectiveId.Trim();

        if (!itemById.TryGetValue(finalId, out MissionObjectiveListItemUI item))
        {
            if (logDebug)
                Debug.LogWarning("[MissionObjectiveListUI] No active UI item found for completed objective id: " + finalId);

            return;
        }

        if (completingIds.Contains(finalId))
            return;

        StartCoroutine(CompleteAndRemoveRoutine(finalId, item));
    }

    public void ClearAllObjectives()
    {
        // Restore/scene reset must not leave old completion coroutines alive.
        StopAllCoroutines();

        completingIds.Clear();
        itemById.Clear();

        for (int i = activeItems.Count - 1; i >= 0; i--)
        {
            if (activeItems[i] != null)
                Destroy(activeItems[i].gameObject);
        }

        activeItems.Clear();

        if (keepTemplateInactive && objectiveItemPrefab != null && objectiveItemPrefab.gameObject.scene.IsValid())
            objectiveItemPrefab.gameObject.SetActive(false);
    }

    /// <summary>
    /// Removes current active objectives that are NOT already playing their
    /// completion animation.
    ///
    /// Used when MissionFlowManager moves to another element immediately after
    /// an objective completed. The old completed row is kept alive so its
    /// green + slide-out animation can finish normally.
    /// </summary>
    public void ClearNonCompletingObjectives()
    {
        itemById.Clear();

        for (int i = activeItems.Count - 1; i >= 0; i--)
        {
            MissionObjectiveListItemUI item = activeItems[i];

            if (item == null)
            {
                activeItems.RemoveAt(i);
                continue;
            }

            if (completingIds.Contains(item.ObjectiveId))
                continue;

            Destroy(item.gameObject);
            activeItems.RemoveAt(i);
        }

        // Rebuild dictionary for the rows whose completion animation is alive.
        for (int i = 0; i < activeItems.Count; i++)
        {
            MissionObjectiveListItemUI item = activeItems[i];

            if (item != null &&
                !string.IsNullOrWhiteSpace(item.ObjectiveId))
            {
                itemById[item.ObjectiveId] = item;
            }
        }

        RepositionItems(false);
    }

    private IEnumerator CompleteAndRemoveRoutine(string objectiveId, MissionObjectiveListItemUI item)
    {
        completingIds.Add(objectiveId);
        item.MarkCompleted(completedTextColor);

        if (logDebug)
            Debug.Log("[MissionObjectiveListUI] Completed objective UI: " + objectiveId);

        yield return new WaitForSeconds(completeDelay);

        // Remove khỏi list trước để các item bên dưới bắt đầu slide lên cùng lúc item complete slide out sang trái.
        activeItems.Remove(item);
        itemById.Remove(objectiveId);
        RepositionItems(true);

        RectTransform rt = item.RectTransform;
        if (rt != null)
        {
            Vector2 outPosition = rt.anchoredPosition + Vector2.left * slideOutLeftDistance;
            yield return item.MoveRoutine(outPosition, slideOutDuration);
        }

        completingIds.Remove(objectiveId);
        item.DestroySelf();
    }

    private void RepositionItems(bool animate)
    {
        for (int i = 0; i < activeItems.Count; i++)
        {
            MissionObjectiveListItemUI item = activeItems[i];
            if (item == null)
                continue;

            Vector2 targetPosition = GetPositionForIndex(i);

            if (animate)
            {
                item.MoveTo(targetPosition, layoutMoveDuration);
            }
            else if (item.RectTransform != null)
            {
                item.RectTransform.anchoredPosition = targetPosition;
            }
        }
    }

    private Vector2 GetPositionForIndex(int index)
    {
        Vector2 firstPosition = firstItemAnchoredPosition;

        if (useTemplatePositionAsFirstPosition && hasCachedTemplatePosition)
            firstPosition = cachedTemplateAnchoredPosition;

        return firstPosition + Vector2.down * itemSpacingY * index;
    }

    private string NormalizeId(string objectiveId, string objectiveText)
    {
        if (!string.IsNullOrWhiteSpace(objectiveId))
            return objectiveId.Trim();

        return string.IsNullOrWhiteSpace(objectiveText) ? "Objective_" + Time.frameCount : objectiveText.Trim();
    }
}
