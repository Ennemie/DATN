using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class InteractController : MonoBehaviour
{
    public enum InteractType
    {   
        Door,
        Commander,
        ActivateSwitch,
        NextScene
    }
    public InteractType interactType;
    private GameObject canvas;
    private TMP_Text message;
    [SerializeField]private ConversationController conversationBox;
    [SerializeField] private ActivateObject activateObject;
    [SerializeField] private string startMessage;
    [SerializeField] private string endMessage;
    [SerializeField] private string nextSceneName;
    void Start()
    {
        canvas = transform.Find("Canvas").gameObject;
        message = canvas.GetComponentInChildren<TMP_Text>();
        message.text = startMessage;
        canvas.SetActive(false);
        if(conversationBox != null ) conversationBox.EnableConversationBox(false);
    }
    public void DoorInteract()
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
    public void CommanderTalk()
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
    public void ActivateSwitch()
    {
        if (activateObject == null) Debug.Log("activate obj null!");
        if (activateObject != null) activateObject.Activate();
    }
    public void NextScene()
    {
        SceneManager.LoadScene(nextSceneName);
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
