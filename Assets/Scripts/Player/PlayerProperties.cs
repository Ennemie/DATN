using UnityEngine;

public class PlayerProperties : MonoBehaviour
{
    public static PlayerProperties Instance { get; private set; }

    private int hp;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
