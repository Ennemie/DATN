// ============================================================================
// GameSaveData.cs
// ============================================================================
// Chức năng:
// - Data model thuần dùng để serialize persistent save game thành JSON.
// - Không chứa MonoBehaviour.
// - Không trực tiếp quản lý Scene / Player / Enemy runtime.
//
// MISSION PERSISTENCE:
// - NotStarted  = chưa được assign / chưa được kích hoạt.
// - Started     = đã được kích hoạt và đang có thể thực hiện.
// - Completed   = đã hoàn thành.
// - Objective không cần lưu NotStarted vào JSON; nếu không xuất hiện trong
//   ObjectiveStates thì mặc định là NotStarted.
//
// SAVE RULE:
// - Data chỉ trở thành persistent khi CheckpointManager commit checkpoint.
// - Runtime mission progress có thể thay đổi sau checkpoint nhưng chưa được
//   ghi vào file cho tới checkpoint tiếp theo.
// ============================================================================

using System;
using System.Collections.Generic;

public enum MissionProgressState
{
    NotStarted = 0,
    Started = 1,
    Completed = 2
}

[Serializable]
public class MissionObjectiveSaveData
{
    public string ObjectiveId;
    public MissionProgressState State = MissionProgressState.NotStarted;

    // Dùng để rebuild Canvas UI mà không phải phụ thuộc popup New Objective.
    public string DisplayText;

    // false = Objective có Started state nhưng không nằm trong active Canvas list.
    public bool ShowInActiveList = true;

    public MissionObjectiveSaveData()
    {
    }

    public MissionObjectiveSaveData(
        string objectiveId,
        MissionProgressState state,
        string displayText,
        bool showInActiveList = true)
    {
        ObjectiveId = objectiveId;
        State = state;
        DisplayText = displayText;
        ShowInActiveList = showInActiveList;
    }

    public MissionObjectiveSaveData Clone()
    {
        return new MissionObjectiveSaveData(
            ObjectiveId,
            State,
            DisplayText,
            ShowInActiveList
        );
    }
}

[Serializable]
public class MissionProgressSaveData
{
    public string MissionId;
    public MissionProgressState State = MissionProgressState.NotStarted;

    // -1 = không có element active.
    public int CurrentElementIndex = -1;

    // True khi current element đã complete nhưng flow chưa chuyển sang
    // element tiếp theo (ví dụ Proceed To Next Element After Post Flow = false).
    public bool CurrentElementCompleted;

    public List<MissionObjectiveSaveData> ObjectiveStates =
        new List<MissionObjectiveSaveData>();

    public void EnsureLists()
    {
        if (ObjectiveStates == null)
            ObjectiveStates = new List<MissionObjectiveSaveData>();
    }

    public MissionObjectiveSaveData FindObjective(string objectiveId)
    {
        if (string.IsNullOrWhiteSpace(objectiveId))
            return null;

        EnsureLists();

        for (int i = 0; i < ObjectiveStates.Count; i++)
        {
            MissionObjectiveSaveData state = ObjectiveStates[i];

            if (state == null)
                continue;

            if (string.Equals(
                    state.ObjectiveId,
                    objectiveId,
                    StringComparison.Ordinal))
            {
                return state;
            }
        }

        return null;
    }

    public MissionProgressSaveData Clone()
    {
        EnsureLists();

        MissionProgressSaveData clone = new MissionProgressSaveData
        {
            MissionId = MissionId,
            State = State,
            CurrentElementIndex = CurrentElementIndex,
            CurrentElementCompleted = CurrentElementCompleted,
            ObjectiveStates = new List<MissionObjectiveSaveData>()
        };

        for (int i = 0; i < ObjectiveStates.Count; i++)
        {
            if (ObjectiveStates[i] != null)
                clone.ObjectiveStates.Add(
                    ObjectiveStates[i].Clone()
                );
        }

        return clone;
    }
}

[Serializable]
public class GameSaveData
{
    public int SaveVersion = 2;

    // Checkpoint progression.
    public int CurrentMap = 0;
    public int CurrentCheckpoint = 0;

    // Player state được commit tại checkpoint.
    public int PlayerHealth = 100;

    // Mission persistence.
    public List<MissionProgressSaveData> MissionStates =
        new List<MissionProgressSaveData>();

    // Các list legacy / helper vẫn giữ lại để API hiện tại không bị phá.
    // MissionStates mới là nguồn dữ liệu chi tiết hơn cho mission flow.
    public List<string> CompletedMissionIds =
        new List<string>();

    public List<string> CompletedObjectiveIds =
        new List<string>();

    // Trigger one-time đã được commit.
    // Dùng cho MissionElementTrigger / trigger mission tương tự.
    public List<string> PersistentTriggeredTriggerIds =
        new List<string>();

    // Enemy đã chết vĩnh viễn và đã được commit qua checkpoint.
    public List<string> PermanentDeadEnemyIds =
        new List<string>();

    public bool HasCommittedCheckpoint
    {
        get
        {
            return CurrentMap > 0 &&
                   CurrentCheckpoint > 0;
        }
    }

    public void EnsureLists()
    {
        if (MissionStates == null)
            MissionStates = new List<MissionProgressSaveData>();

        if (CompletedMissionIds == null)
            CompletedMissionIds = new List<string>();

        if (CompletedObjectiveIds == null)
            CompletedObjectiveIds = new List<string>();

        if (PersistentTriggeredTriggerIds == null)
            PersistentTriggeredTriggerIds = new List<string>();

        if (PermanentDeadEnemyIds == null)
            PermanentDeadEnemyIds = new List<string>();

        for (int i = 0; i < MissionStates.Count; i++)
        {
            MissionProgressSaveData missionState =
                MissionStates[i];

            if (missionState == null)
                continue;

            missionState.EnsureLists();

            // Legacy/transition safety:
            // if an active Mission already contains Started objectives, the
            // current element cannot be treated as fully completed.
            if (missionState.State ==
                    MissionProgressState.Started &&
                missionState.CurrentElementCompleted)
            {
                for (int j = 0;
                     j < missionState.ObjectiveStates.Count;
                     j++)
                {
                    MissionObjectiveSaveData objectiveState =
                        missionState.ObjectiveStates[j];

                    if (objectiveState != null &&
                        objectiveState.State ==
                            MissionProgressState.Started)
                    {
                        missionState.CurrentElementCompleted = false;
                        break;
                    }
                }
            }

            if (missionState.State ==
                MissionProgressState.Completed)
            {
                missionState.CurrentElementIndex = -1;
                missionState.CurrentElementCompleted = false;
            }

            if (missionState.State ==
                MissionProgressState.NotStarted)
            {
                missionState.CurrentElementIndex = -1;
                missionState.CurrentElementCompleted = false;
            }
        }
    }

    public MissionProgressSaveData GetMissionState(string missionId)
    {
        if (string.IsNullOrWhiteSpace(missionId))
            return null;

        EnsureLists();

        for (int i = 0; i < MissionStates.Count; i++)
        {
            MissionProgressSaveData state = MissionStates[i];

            if (state == null)
                continue;

            if (string.Equals(
                    state.MissionId,
                    missionId,
                    StringComparison.Ordinal))
            {
                return state;
            }
        }

        return null;
    }

    public void UpsertMissionState(MissionProgressSaveData state)
    {
        if (state == null ||
            string.IsNullOrWhiteSpace(state.MissionId))
        {
            return;
        }

        EnsureLists();
        state.EnsureLists();

        for (int i = 0; i < MissionStates.Count; i++)
        {
            if (MissionStates[i] == null)
                continue;

            if (!string.Equals(
                    MissionStates[i].MissionId,
                    state.MissionId,
                    StringComparison.Ordinal))
            {
                continue;
            }

            MissionStates[i] = state.Clone();
            SyncLegacyMissionListsFromMissionState(state);
            return;
        }

        MissionStates.Add(state.Clone());
        SyncLegacyMissionListsFromMissionState(state);
    }

    private void SyncLegacyMissionListsFromMissionState(
        MissionProgressSaveData state)
    {
        if (state == null)
            return;

        EnsureLists();

        if (state.State == MissionProgressState.Completed)
        {
            if (!CompletedMissionIds.Contains(state.MissionId))
                CompletedMissionIds.Add(state.MissionId);
        }
        else
        {
            CompletedMissionIds.Remove(state.MissionId);
        }

        for (int i = 0; i < state.ObjectiveStates.Count; i++)
        {
            MissionObjectiveSaveData objectiveState =
                state.ObjectiveStates[i];

            if (objectiveState == null ||
                string.IsNullOrWhiteSpace(objectiveState.ObjectiveId))
            {
                continue;
            }

            if (objectiveState.State == MissionProgressState.Completed)
            {
                if (!CompletedObjectiveIds.Contains(
                        objectiveState.ObjectiveId))
                {
                    CompletedObjectiveIds.Add(
                        objectiveState.ObjectiveId
                    );
                }
            }
            else
            {
                CompletedObjectiveIds.Remove(
                    objectiveState.ObjectiveId
                );
            }
        }
    }

    public bool IsMissionCompleted(string missionId)
    {
        if (string.IsNullOrWhiteSpace(missionId))
            return false;

        MissionProgressSaveData missionState =
            GetMissionState(missionId);

        if (missionState != null &&
            missionState.State == MissionProgressState.Completed)
        {
            return true;
        }

        return CompletedMissionIds.Contains(missionId);
    }

    public bool IsObjectiveCompleted(string objectiveId)
    {
        if (string.IsNullOrWhiteSpace(objectiveId))
            return false;

        EnsureLists();

        for (int i = 0; i < MissionStates.Count; i++)
        {
            MissionProgressSaveData missionState =
                MissionStates[i];

            if (missionState == null)
                continue;

            MissionObjectiveSaveData objectiveState =
                missionState.FindObjective(objectiveId);

            if (objectiveState == null)
                continue;

            return objectiveState.State ==
                   MissionProgressState.Completed;
        }

        return CompletedObjectiveIds.Contains(objectiveId);
    }

    public MissionProgressState GetObjectiveState(
        string objectiveId)
    {
        if (string.IsNullOrWhiteSpace(objectiveId))
            return MissionProgressState.NotStarted;

        EnsureLists();

        for (int i = 0; i < MissionStates.Count; i++)
        {
            MissionProgressSaveData missionState =
                MissionStates[i];

            if (missionState == null)
                continue;

            MissionObjectiveSaveData objectiveState =
                missionState.FindObjective(objectiveId);

            if (objectiveState != null)
                return objectiveState.State;
        }

        return CompletedObjectiveIds.Contains(objectiveId)
            ? MissionProgressState.Completed
            : MissionProgressState.NotStarted;
    }

    public GameSaveData Clone()
    {
        EnsureLists();

        GameSaveData clone = new GameSaveData
        {
            SaveVersion = SaveVersion,
            CurrentMap = CurrentMap,
            CurrentCheckpoint = CurrentCheckpoint,
            PlayerHealth = PlayerHealth,
            MissionStates = new List<MissionProgressSaveData>(),
            CompletedMissionIds = new List<string>(
                CompletedMissionIds
            ),
            CompletedObjectiveIds = new List<string>(
                CompletedObjectiveIds
            ),
            PersistentTriggeredTriggerIds =
                new List<string>(
                    PersistentTriggeredTriggerIds
                ),
            PermanentDeadEnemyIds =
                new List<string>(
                    PermanentDeadEnemyIds
                )
        };

        for (int i = 0; i < MissionStates.Count; i++)
        {
            if (MissionStates[i] != null)
            {
                clone.MissionStates.Add(
                    MissionStates[i].Clone()
                );
            }
        }

        return clone;
    }
}
