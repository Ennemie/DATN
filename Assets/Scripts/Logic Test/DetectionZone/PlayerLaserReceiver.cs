using UnityEngine;

public class PlayerLaserReceiver : MonoBehaviour
{
    [SerializeField] private bool detectedByLaser;

    public bool DetectedByLaser => detectedByLaser;

    public void SetDetected(bool value)
    {
        detectedByLaser = value;
    }

    public void ResetLaserState()
    {
        detectedByLaser = false;
    }
}