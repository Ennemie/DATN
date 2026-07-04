using System.Collections.Generic;
using UnityEngine;

public class UnityMainThreadDispatcher : MonoBehaviour
{
    private static UnityMainThreadDispatcher _instance;
    private readonly Queue<System.Action> _executionQueue = new Queue<System.Action>();

    public void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void Update()
    {
        lock (_executionQueue)
        {
            while (_executionQueue.Count > 0)
            {
                _executionQueue.Dequeue().Invoke();
            }
        }
    }

    public static void Enqueue(System.Collections.IEnumerator action)
    {
        if (_instance == null)
        {
            Debug.LogError("Chưa có UnityMainThreadDispatcher trong Scene. Hãy tạo 1 GameObject trống và gắn script này vào.");
            return;
        }

        lock (_instance._executionQueue)
        {
            _instance._executionQueue.Enqueue(() => { _instance.StartCoroutine(action); });
        }
    }
}
