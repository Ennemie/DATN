// Chức năng: Khóa/mở input player theo dạng lock counter để tránh hội thoại/camera/fail mở input sai thời điểm.
// Gán optional lên Player hoặc Managers. DialogueController/MissionCameraDirector có thể gọi Lock/Unlock.
using System.Collections.Generic;
using UnityEngine;

public class PlayerInputLockController : MonoBehaviour
{
    [Header("Behaviours To Disable When Locked")]
    [SerializeField] private Behaviour[] behavioursToDisable;

    [Header("Optional Objects To Hide When Locked")]
    [SerializeField] private GameObject[] objectsToHideWhenLocked;

    [Header("Debug")]
    [SerializeField] private bool logDebug;

    private readonly HashSet<string> activeLocks = new HashSet<string>();

    public bool IsLocked => activeLocks.Count > 0;

    public void Lock(string reason)
    {
        string key = Normalize(reason);
        bool wasLocked = IsLocked;
        activeLocks.Add(key);

        if (!wasLocked && IsLocked)
            ApplyLockedState(true);

        if (logDebug)
            Debug.Log("[PlayerInputLockController] Lock: " + key + " / count = " + activeLocks.Count, this);
    }

    public void Unlock(string reason)
    {
        string key = Normalize(reason);
        bool removed = activeLocks.Remove(key);

        if (removed && !IsLocked)
            ApplyLockedState(false);

        if (logDebug)
            Debug.Log("[PlayerInputLockController] Unlock: " + key + " / count = " + activeLocks.Count, this);
    }

    public void ClearAllLocks()
    {
        activeLocks.Clear();
        ApplyLockedState(false);
    }

    private void ApplyLockedState(bool locked)
    {
        if (behavioursToDisable != null)
        {
            for (int i = 0; i < behavioursToDisable.Length; i++)
            {
                if (behavioursToDisable[i] != null)
                    behavioursToDisable[i].enabled = !locked;
            }
        }

        if (objectsToHideWhenLocked != null)
        {
            for (int i = 0; i < objectsToHideWhenLocked.Length; i++)
            {
                if (objectsToHideWhenLocked[i] != null)
                    objectsToHideWhenLocked[i].SetActive(!locked);
            }
        }
    }

    private string Normalize(string reason)
    {
        return string.IsNullOrWhiteSpace(reason) ? "LOCK" : reason.Trim();
    }
}
