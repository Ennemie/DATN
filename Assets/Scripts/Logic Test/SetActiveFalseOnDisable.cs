using UnityEngine;

public class SetActiveFalseOnDisable : MonoBehaviour
{
    public GameObject[] listActivate;
    public GameObject[] listDeactivate;

    private void OnDisable()
    {
        // Bat cac object trong danh sach
        if (listActivate != null)
        {
            foreach (GameObject obj in listActivate)
            {
                if (obj != null)
                {
                    obj.SetActive(true);
                }
            }
        }

        // Tat cac object trong danh sach
        if (listDeactivate != null)
        {
            foreach (GameObject obj in listDeactivate)
            {
                if (obj != null)
                {
                    obj.SetActive(false);
                }
            }
        }
    }
}