using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using DG.Tweening;
using Unity.AppUI.UI;
using DG.Tweening.Core;

public class ConversationController : MonoBehaviour
{
    [SerializeField] private int missionIndex;

    [SerializeField] private NPC currentNPC;
    public enum NPC
    {
        None,
        Commander
    }

    private Vector2 leftBtnOriginalPos = new Vector2(0, -165.5f);
    private Vector2 leftBtnTargetPos = new Vector2(-235f, -165.5f);
    private Vector2 rightBtnOriginalPos = new Vector2(0, -165.5f);
    private Vector2 rightBtnTargetPos = new Vector2(235f, -165.5f);

    [SerializeField] private RectTransform leftBtn;
    [SerializeField] private RectTransform rightBtn;
    [SerializeField] private CanvasGroup lineBox;
    [SerializeField] private TMP_Text npcNameText;
    [SerializeField] private TMP_Text conversationLine;
    private int conversationIndex;

    [SerializeField] private List<ConversationStruct> conversationLines;
    private Coroutine nextLineCoroutine;

    public void EnableConversationBox(bool enable)
    {
        gameObject.SetActive(enable);
    }
    public void EndConversation()
    {
        DisplayConversationLine(conversationLines.Count - 1);
        StartCoroutine(WaitToClose(1.5f));
    }
    void OnEnable()
    {
        conversationIndex = 0;
        ShowConversationBox(true);
        PlayerCanvasController.Instance.Enable(false);
    }
    void OnDisable()
    {
        PlayerState.Instance.CurrentState = PlayerState.State.Idle;
        PlayerController.Instance.acceptInput = true;

        if(currentNPC == NPC.Commander)
        {
            CommanderController.Instance.state = CommanderController.State.Idle;
        }
        PlayerCanvasController.Instance.Enable(true);
    }
    public void NextConversationLine()
    {
        if (nextLineCoroutine != null)
        {
            StopCoroutine(nextLineCoroutine);
        }
        conversationIndex++;
        DisplayConversationLine(conversationIndex);
    }
    private void DisplayConversationLine(int index)
    {
        if (index >= conversationLines.Count)
        {
            MissionManager.instance.CompleteCurrentMission(missionIndex);
            ShowConversationBox(false);
        }
        else
        {
            npcNameText.text = conversationLines[index].name;
            conversationLine.text = conversationLines[index].conversationLine;
            nextLineCoroutine = StartCoroutine(WaitForNextLine(4f));
        }
    }
    private IEnumerator WaitToClose(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        ShowConversationBox(false);
    }
    private IEnumerator WaitForNextLine(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        NextConversationLine();
    }
    private void ShowConversationBox(bool show)
    {
        if (show)
        {
            leftBtn.anchoredPosition = leftBtnOriginalPos;
            rightBtn.anchoredPosition = rightBtnOriginalPos;
            lineBox.alpha = 0;

            DisplayConversationLine(conversationIndex);
            leftBtn.DOAnchorPos(leftBtnTargetPos, 1).SetEase(Ease.OutBack);
            rightBtn.DOAnchorPos(rightBtnTargetPos, 1).SetEase(Ease.OutBack);
            lineBox.DOFade(1, 1f);
        }
        else
        {
            leftBtn.DOAnchorPos(leftBtnOriginalPos, 0.5f).SetEase(Ease.InBack);
            rightBtn.DOAnchorPos(rightBtnOriginalPos, 0.5f).SetEase(Ease.InBack);
            lineBox.DOFade(0, 0.5f).OnComplete(() => gameObject.SetActive(false));
        }
    }
}
