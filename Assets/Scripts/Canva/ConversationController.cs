using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using DG.Tweening;

public class ConversationController : MonoBehaviour
{
    [SerializeField] private NPC currentNPC;
    public enum NPC
    {
        None,
        Commander
    }
    [SerializeField] private TMP_Text npcNameText;
    [SerializeField] private TMP_Text conversationLine;
    private int conversationIndex;

    [SerializeField] private List<ConversationStruct> conversationLines;
    private Coroutine conversationCoroutine;

    public void EnableConversationBox(bool enable)
    {
        gameObject.SetActive(enable);
    }
    public void EndConversation()
    {
        DisplayConversationLine(conversationLines.Count - 1);
        StartCoroutine(WaitToClose(1f));
    }
    void OnEnable()
    {
        PlayerCanvasController.Instance.Enable(false);
        conversationIndex = 0;
        DisplayConversationLine(conversationIndex);
    }
    void OnDisable()
    {
        PlayerCanvasController.Instance.Enable(true);
        PlayerState.Instance.CurrentState = PlayerState.State.Idle;
        PlayerController.Instance.acceptInput = true;

        if(currentNPC == NPC.Commander)
        {
            CommanderController.Instance.state = CommanderController.State.Idle;
        }
    }
    public void NextConversationLine()
    {
        conversationIndex++;
        StopCoroutine(conversationCoroutine);
        DisplayConversationLine(conversationIndex);
    }
    private void DisplayConversationLine(int index)
    {
        if (index >= conversationLines.Count)
        {
            gameObject.SetActive(false);
        }
        else
        {
            npcNameText.text = conversationLines[index].name;
            conversationLine.text = conversationLines[index].conversationLine;
            conversationCoroutine = StartCoroutine(WaitToNextLine(3f));
        }
    }
    private IEnumerator WaitToNextLine(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        NextConversationLine();
    }
    private IEnumerator WaitToClose(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        gameObject.SetActive(false);
    }
}
