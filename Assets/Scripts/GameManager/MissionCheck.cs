using System;
using Unity.VisualScripting;
using UnityEngine;

public class MissionCheck : MonoBehaviour
{
    [SerializeField] private int missionIndex;

    private bool _isMissionComplete;
    [HideInInspector] public bool isMissionComplete
    {
        get { return _isMissionComplete; }
        set
        {
            if(value == true && !_isMissionComplete)
            {
                _isMissionComplete = true;
                SendMissionComplete();
            }
        }
    }

    [SerializeField] private bool isSceneComplete = false;
    [SerializeField] private string nextSceneName;

    private void SendMissionComplete()
    {
        MissionManager.instance.CompleteCurrentMission(missionIndex, isSceneComplete, nextSceneName);
    }
}
