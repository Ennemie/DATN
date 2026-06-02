using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class DoorV2Controller : MonoBehaviour
{
    [SerializeField] private GameObject doorPart1;
    [SerializeField] private GameObject doorPart2;
    [SerializeField] private float openAngle;
    [SerializeField] private float speed;

    private Vector3 closePos;
    private Quaternion closeRot;
    private Vector3 openPos;
    private Quaternion openRot;
    private Vector3 targetPos;
    private Quaternion targetRot;
    private float offset = 0.45f;

    private bool _isOpen = false;
    [HideInInspector] public bool isOpen
    {
        get { return _isOpen; }
        set
        {
            if (value != _isOpen)
            {
                _isOpen = value;
                if (_isOpen)
                {
                    OpenDoor();
                }
                else
                {
                    CloseDoor();
                }
            }
        }
    }
    private void Start()
    {
        closePos = doorPart2.transform.localPosition;
        closeRot = doorPart2.transform.localRotation;
        openPos = closePos + new Vector3(-offset, 0, offset);
        openRot = closeRot * Quaternion.Euler(0, openAngle, 0);
        if (isOpen)
        {
            targetPos = openPos;
            targetRot = openRot;
        }
        else
        {
            targetPos = closePos;
            targetRot = closeRot;
        }
    }
    void FixedUpdate()
    {
        doorPart2.transform.localPosition = Vector3.Lerp(doorPart2.transform.localPosition, targetPos, Time.fixedDeltaTime * speed);
        doorPart2.transform.localRotation = Quaternion.Lerp(doorPart2.transform.localRotation, targetRot, Time.fixedDeltaTime * speed);
    }
    private void OpenDoor()
    {
        targetPos = openPos;
        targetRot = openRot;
    }

    private void CloseDoor()
    {
        targetPos = closePos;
        targetRot = closeRot;
    }

}
