using UnityEngine;

public class PlayerProperties : MonoBehaviour
{
    public static PlayerProperties Instance { get; private set; }

    private int hp;
    void Start()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
