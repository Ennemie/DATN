    // Chức năng: Hiển thị TMP CheckPoint một lần khi checkpoint được lưu.
    // Gán cho: GameManager hoặc object Managers luôn active.
    // Tham chiếu với: MissionFlowManager gọi ShowCheckpoint() khi SaveCheckpoint(); TMP object checkpoint có thể inactive ban đầu.
    using System.Collections;
    using TMPro;
    using UnityEngine;

    public class MissionCheckpointToast : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject checkpointRoot;
        [SerializeField] private TMP_Text checkpointText;

        [Header("Settings")]
        [SerializeField] private string defaultMessage = "Checkpoint";
        [SerializeField] private float showDuration = 1.5f;
        [SerializeField] private bool hideOnAwake = true;

        private Coroutine showRoutine;

        private void Awake()
        {
            if (hideOnAwake && checkpointRoot != null)
                checkpointRoot.SetActive(false);
        }

        public void ShowCheckpoint(string message)
        {
            if (showRoutine != null)
                StopCoroutine(showRoutine);

            showRoutine = StartCoroutine(ShowRoutine(message));
        }

        private IEnumerator ShowRoutine(string message)
        {
            if (checkpointRoot != null)
                checkpointRoot.SetActive(true);

            if (checkpointText != null)
                checkpointText.text = string.IsNullOrWhiteSpace(message) ? defaultMessage : message;

            yield return new WaitForSeconds(showDuration);

            if (checkpointRoot != null)
                checkpointRoot.SetActive(false);

            showRoutine = null;
        }
    }
