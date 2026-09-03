using UnityEngine;

public class FPSManager : MonoBehaviour
{
    private static FPSManager instance;
    public int FPS;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Awake()
    {
      if (instance != null)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = FPS;
    }

    // Update is called once per frame
   
}
