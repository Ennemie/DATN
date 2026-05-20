using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class InteractController : MonoBehaviour
{
    public enum InteractType
    {   
        Door,
        Commander
    }
    public InteractType interactType;
    private GameObject canvas;
    private TMP_Text message;
    [SerializeField] private string startMessage;
    [SerializeField] private string endMessage;
    void Start()
    {
        canvas = transform.Find("Canvas").gameObject;
        message = canvas.GetComponentInChildren<TMP_Text>();
        message.text = startMessage;
        canvas.SetActive(false);
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
        }
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
