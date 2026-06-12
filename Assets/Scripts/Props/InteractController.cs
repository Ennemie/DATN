using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class InteractController : MonoBehaviour
{
    public enum InteractType
    {   
        Door,
        Commander,
        ActivateSwitch,
        NextScene,
    }
    public InteractType interactType;
    private GameObject canvas;
    private TMP_Text message;
    private MissionCheck missionCheck;

    [Header("Conversation")]
    [SerializeField]private ConversationController conversationBox;

    [Header("Activate Object")]
    [SerializeField] private List<ActivateObject> activateObjects;

    [Header("For canvas")]
    [SerializeField] private string startMessage;
    [SerializeField] private string endMessage;

    [Header("For Next Scene")]
    [SerializeField] private string nextSceneName;
    void Start()
    {
        canvas = transform.Find("Canvas").gameObject;
        message = canvas.GetComponentInChildren<TMP_Text>();
        message.text = startMessage;
        canvas.SetActive(false);
        if(conversationBox != null ) conversationBox.EnableConversationBox(false);
        missionCheck = GetComponent<MissionCheck>();
    }
    public void Interact()
    {
        switch (interactType)
        {
            case InteractType.Door:
                DoorInteract();
                break;
            case InteractType.Commander:
                CommanderTalk();
                break;
            case InteractType.ActivateSwitch:
                ActivateSwitch();
                break;
            case InteractType.NextScene:
                NextScene();
                break;
        }
    }
    private void DoorInteract()
    {

        if (message.text == startMessage) message.text = endMessage;
        else message.text = startMessage;

        TryGetComponent<DoorV1Controller>(out DoorV1Controller door1);
        if (door1 != null)
        {
            if (door1.isOpen) door1.isOpen = false;
            else door1.isOpen=true;
            return;
        }
        TryGetComponent<DoorV2Controller>(out DoorV2Controller door2);
        if (door2 != null)
        {
            if (door2.isOpen) door2.isOpen = false;
            else door2.isOpen=true;
            return;
        }
    }
    private void CommanderTalk()
    {
        canvas.SetActive(false);
        if (CommanderController.Instance.state == CommanderController.State.Sitting)
        {
            CommanderController.Instance.state = CommanderController.State.StandUp;
        }
        else
        {
            CommanderController.Instance.LookAtPlayer();
            CommanderController.Instance.state = CommanderController.State.Talking;
        }
        conversationBox.EnableConversationBox(true);
    }
    private void ActivateSwitch()
    {
        foreach (ActivateObject obj in activateObjects)
        {
            obj.Activate();
        }
    }
    private void NextScene()
    {
        missionCheck.isMissionComplete = true;
        SceneManager.LoadScene(MissionManager.instance.nextSceneName);
    }
    public void NextLineConversation()
    {
        if(conversationBox == null) return;
        conversationBox.NextConversationLine();
    }
    public void ShowMessage()
    {
        canvas.SetActive(true);
    }
    public void HideMessage()
    {
        canvas.SetActive(false);
    }
}
