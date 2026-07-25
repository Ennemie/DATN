// Chức năng: Core chạy hội thoại.
// - Typewriter từng ký tự.
// - Click khi đang typing => hiện full line hiện tại, KHÔNG nhảy line.
// - Click khi line đã full => qua line kế tiếp.
// - Nút skip => bỏ qua toàn bộ conversation hiện tại nếu Conversation AllowSkip = true.
// - Speaker chỉ dùng cho Name/Avatar; camera có mode riêng từng line.
// - Không gọi MissionFlowManager trực tiếp; manager/trigger sẽ đợi coroutine hoặc nghe event.
using System;
using System.Collections;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public enum DialogueFinishReason
{
    Completed,
    Skipped,
    Interrupted
}

public class DialogueController : MonoBehaviour
{
    public static DialogueController Instance { get; private set; }

    [Header("References")]
    [SerializeField] private DialogueUI dialogueUI;
    [SerializeField] private MissionCameraDirector cameraDirector;
    [SerializeField] private PlayerInputLockController inputLockController;

    [Header("Input")]
    [SerializeField] private bool allowMouseClickAnywhere = true;
    [SerializeField] private bool allowSpaceOrEnterToAdvance = true;
    [SerializeField] private bool useUnscaledTime;

    [Header("Behaviour")]
    [SerializeField] private bool interruptCurrentConversationWhenNewStarts = true;
    [SerializeField] private string inputLockReason = "Dialogue";

    [Header("Debug")]
    [SerializeField] private bool logDebug;

    public event Action<DialogueConversationData> ConversationStarted;
    public event Action<DialogueConversationData, DialogueFinishReason> ConversationFinished;

    private DialogueConversationData currentConversation;
    private Coroutine conversationRoutine;
    private bool advanceRequested;
    private bool revealCurrentLineRequested;
    private bool skipAllRequested;
    private bool isTypingLine;
    private bool isWaitingForContinue;
    private DialogueFinishReason lastFinishReason = DialogueFinishReason.Completed;

    public bool IsPlaying => conversationRoutine != null;
    public DialogueConversationData CurrentConversation => currentConversation;
  

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Debug.LogWarning("[DialogueController] More than one instance found. Destroying duplicate on " + name, this);
            Destroy(gameObject);
            return;
        }

        if (dialogueUI == null)
            dialogueUI = FindFirstObjectByType<DialogueUI>();

        if (cameraDirector == null)
            cameraDirector = FindFirstObjectByType<MissionCameraDirector>();

        if (inputLockController == null)
            inputLockController = FindFirstObjectByType<PlayerInputLockController>();
    }

    private void OnEnable()
    {
        if (dialogueUI != null)
        {
            dialogueUI.ScreenClicked += RequestAdvanceFromClick;
            dialogueUI.SkipClicked += RequestSkipAll;
        }
    }

    private void OnDisable()
    {
        if (dialogueUI != null)
        {
            dialogueUI.ScreenClicked -= RequestAdvanceFromClick;
            dialogueUI.SkipClicked -= RequestSkipAll;
        }
    }

    private void Update()
    {
        if (!IsPlaying)
            return;

        if (WasAdvancePressedThisFrame())
            RequestAdvanceFromClick();
    }

    public void PlayConversation(DialogueConversationData conversation)
    {
        if (conversation == null)
        {
            Debug.LogWarning("[DialogueController] PlayConversation ignored because conversation is null.", this);
            return;
        }

        if (conversationRoutine != null)
        {
            if (!interruptCurrentConversationWhenNewStarts)
            {
                Debug.LogWarning("[DialogueController] Conversation is already playing. New conversation ignored: " + conversation.ConversationId, this);
                return;
            }

            StopCoroutine(conversationRoutine);
            FinishConversation(DialogueFinishReason.Interrupted, false);
        }

        conversationRoutine = StartCoroutine(PlayConversationRoutineInternal(conversation));
    }

    public IEnumerator PlayConversationRoutine(DialogueConversationData conversation)
    {
        PlayConversation(conversation);

        while (IsPlaying)
            yield return null;
    }

    public void RequestAdvanceFromClick()
    {
        if (!IsPlaying)
            return;

        if (isTypingLine)
        {
            revealCurrentLineRequested = true;
            return;
        }

        if (isWaitingForContinue)
            advanceRequested = true;
    }

    public void RequestSkipAll()
    {
        if (!IsPlaying || currentConversation == null || !currentConversation.AllowSkip)
            return;

        skipAllRequested = true;
    }

    private IEnumerator PlayConversationRoutineInternal(DialogueConversationData conversation)
    {
        currentConversation = conversation;
        lastFinishReason = DialogueFinishReason.Completed;
        advanceRequested = false;
        revealCurrentLineRequested = false;
        skipAllRequested = false;

        if (logDebug)
            Debug.Log("[DialogueController] Start conversation: " + conversation.ConversationId, conversation);

        inputLockController?.Lock(inputLockReason);
        cameraDirector?.BeginCameraControl();

        if (dialogueUI != null)
        {
            dialogueUI.Show();
            dialogueUI.ConfigureSkip(conversation.AllowSkip, conversation.SkipButtonText);
            dialogueUI.SetContinueHintVisible(false);
        }

        ConversationStarted?.Invoke(conversation);

        DialogueLine[] lines = conversation.Lines;
        if (lines != null)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                if (skipAllRequested)
                {
                    lastFinishReason = DialogueFinishReason.Skipped;
                    break;
                }

                DialogueLine line = lines[i];
                if (line == null)
                    continue;

                yield return PlayLineRoutine(conversation, line);
            }
        }

        if (lastFinishReason == DialogueFinishReason.Completed && skipAllRequested)
            lastFinishReason = DialogueFinishReason.Skipped;

        if (conversation.ReturnCameraToPlayerOnEnd && cameraDirector != null)
            yield return cameraDirector.ReturnToPlayer(conversation.ReturnCameraMoveTime);

        FinishConversation(lastFinishReason, true);
    }

    private IEnumerator PlayLineRoutine(DialogueConversationData conversation, DialogueLine line)
    {
        revealCurrentLineRequested = false;
        advanceRequested = false;
        isTypingLine = false;
        isWaitingForContinue = false;

        if (dialogueUI != null)
        {
            dialogueUI.SetContinueHintVisible(false);
            dialogueUI.SetActor(line.speaker);
            dialogueUI.SetContent(string.Empty);
        }

        IEnumerator cameraRoutine = RunLineCameraRoutine(line);
        if (line.waitForCameraBeforeTyping)
        {
            yield return cameraRoutine;
        }
        else if (cameraRoutine != null)
        {
            StartCoroutine(cameraRoutine);
        }

        if (skipAllRequested)
        {
            lastFinishReason = DialogueFinishReason.Skipped;
            yield break;
        }

        string content = line.content ?? string.Empty;
        float secondsPerCharacter = ResolveSecondsPerCharacter(conversation, line);

        isTypingLine = true;

        if (secondsPerCharacter <= 0f || content.Length == 0)
        {
            dialogueUI?.SetContent(content);
        }
        else
        {
            for (int i = 0; i < content.Length; i++)
            {
                if (skipAllRequested)
                {
                    lastFinishReason = DialogueFinishReason.Skipped;
                    yield break;
                }

                if (revealCurrentLineRequested)
                {
                    dialogueUI?.SetContent(content);
                    break;
                }

                dialogueUI?.SetContent(content.Substring(0, i + 1));
                yield return WaitSeconds(secondsPerCharacter);
            }
        }

        isTypingLine = false;
        revealCurrentLineRequested = false;
        dialogueUI?.SetContent(content);

        if (line.waitAfterTypingBeforeContinue > 0f)
            yield return WaitSeconds(line.waitAfterTypingBeforeContinue);

        if (skipAllRequested)
        {
            lastFinishReason = DialogueFinishReason.Skipped;
            yield break;
        }

        isWaitingForContinue = true;
        dialogueUI?.SetContinueHintVisible(true);

        while (!advanceRequested && !skipAllRequested)
            yield return null;

        dialogueUI?.SetContinueHintVisible(false);
        isWaitingForContinue = false;
        advanceRequested = false;

        if (skipAllRequested)
            lastFinishReason = DialogueFinishReason.Skipped;
    }

    private IEnumerator RunLineCameraRoutine(DialogueLine line)
    {
        if (cameraDirector == null || line == null)
            yield break;

        switch (line.cameraMode)
        {
            case DialogueCameraMode.None:
            case DialogueCameraMode.KeepCurrent:
                yield break;

            case DialogueCameraMode.SpeakerDefault:
            {
                Transform target = DialogueActorRegistry.GetDefaultCameraTarget(line.speaker);
                if (target != null)
                    yield return cameraDirector.MoveToTarget(target, line.cameraMoveTime);
                yield break;
            }

            case DialogueCameraMode.CustomTarget:
                if (line.customCameraTarget != null)
                    yield return cameraDirector.MoveToTarget(line.customCameraTarget, line.cameraMoveTime);
                yield break;

            case DialogueCameraMode.ReturnToPlayer:
                yield return cameraDirector.ReturnToPlayer(line.cameraMoveTime);
                yield break;
        }
    }

    private void FinishConversation(DialogueFinishReason reason, bool invokeEvent)
    {
        DialogueConversationData finishedConversation = currentConversation;

        if (dialogueUI != null)
        {
            dialogueUI.SetContinueHintVisible(false);
            dialogueUI.HideImmediate();
        }

        currentConversation = null;
        conversationRoutine = null;
        isTypingLine = false;
        isWaitingForContinue = false;
        advanceRequested = false;
        revealCurrentLineRequested = false;
        skipAllRequested = false;

        cameraDirector?.EndCameraControl();
        inputLockController?.Unlock(inputLockReason);

        if (invokeEvent && finishedConversation != null)
            ConversationFinished?.Invoke(finishedConversation, reason);

        if (logDebug && finishedConversation != null)
            Debug.Log("[DialogueController] Finish conversation: " + finishedConversation.ConversationId + " / reason = " + reason, finishedConversation);
    }

    private float ResolveSecondsPerCharacter(DialogueConversationData conversation, DialogueLine line)
    {
        if (line.secondsPerCharacterOverride > 0f)
            return line.secondsPerCharacterOverride;

        if (line.speaker != null && line.speaker.DefaultSecondsPerCharacter > 0f)
            return line.speaker.DefaultSecondsPerCharacter;

        return conversation != null ? Mathf.Max(0f, conversation.DefaultSecondsPerCharacter) : 0.035f;
    }

    private IEnumerator WaitSeconds(float seconds)
    {
        if (seconds <= 0f)
            yield break;

        if (useUnscaledTime)
            yield return new WaitForSecondsRealtime(seconds);
        else
            yield return new WaitForSeconds(seconds);
    }

    private bool WasAdvancePressedThisFrame()
    {
        bool pressed = false;

#if ENABLE_LEGACY_INPUT_MANAGER
        if (allowMouseClickAnywhere)
            pressed |= Input.GetMouseButtonDown(0);

        if (allowSpaceOrEnterToAdvance)
            pressed |= Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return);
#endif

#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        Keyboard keyboard = Keyboard.current;

        if (allowMouseClickAnywhere && mouse != null)
            pressed |= mouse.leftButton.wasPressedThisFrame;

        if (allowSpaceOrEnterToAdvance && keyboard != null)
            pressed |= keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame;
#endif

        return pressed;
    }
}
