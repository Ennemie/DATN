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
    [SerializeField] private TMP_Text name;
    [SerializeField] private TMP_Text conversationLine;
    private int conversationIndex;

    [SerializeField] private List<ConversationStruct> conversationLines;

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
        conversationIndex = 0;
        DisplayConversationLine(conversationIndex);
    }
    private void OnDisable()
    {
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
            name.text = conversationLines[index].name;
            conversationLine.text = conversationLines[index].conversationLine;
        }
    }
    private IEnumerator WaitToClose(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        gameObject.SetActive(false);
    }
}
