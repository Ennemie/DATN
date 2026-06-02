using UnityEngine;
using UnityEngine.InputSystem;

public class DoorV1Controller : MonoBehaviour
{
    [SerializeField] private GameObject doorObj;
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
    [HideInInspector]
    public bool isOpen
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
        closePos = doorObj.transform.localPosition;
        closeRot = doorObj.transform.localRotation;
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
        doorObj.transform.localPosition = Vector3.Lerp(doorObj.transform.localPosition, targetPos, Time.fixedDeltaTime * speed);
        doorObj.transform.localRotation = Quaternion.Lerp(doorObj.transform.localRotation, targetRot, Time.fixedDeltaTime * speed);
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
