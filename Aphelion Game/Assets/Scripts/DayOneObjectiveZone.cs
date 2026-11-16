
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class DayOneObjectiveZone : MonoBehaviour
{
    [Header("Objective")]
    [Range(0, 3)]
    [SerializeField] private int objectiveIndex;

    [SerializeField] private string sectionName;

    [TextArea(2, 5)]
    [SerializeField] private string sectionDescription;

    [Header("References")]
    [SerializeField] private MissionDayManager dayManager;
    [SerializeField] private DayOneMissionManager missionManager;
    [SerializeField] private DayOneMissionUI missionUI;

    [Header("Red Beacon")]
    [SerializeField] private GameObject redBeacon;
    [SerializeField] private bool showBeacon = false;

    private bool localPlayerHasEntered;

    private void Reset()
    {
        GetComponent<BoxCollider>().isTrigger = true;
    }

    private void Awake()
    {
        if (dayManager == null)
            dayManager = FindFirstObjectByType<MissionDayManager>();

        if (missionManager == null)
            missionManager = FindFirstObjectByType<DayOneMissionManager>();

        if (missionUI == null)
            missionUI = FindFirstObjectByType<DayOneMissionUI>();

        if (redBeacon != null)
            redBeacon.SetActive(showBeacon);
    }

    private void OnTriggerEnter(Collider other)
    {
        NetworkObject player =
            other.GetComponentInParent<NetworkObject>();

        if (player == null || !player.IsOwner)
            return;

        if (localPlayerHasEntered)
            return;

        if (dayManager == null ||
            missionManager == null)
        {
            Debug.LogError(
                "[OBJECTIVE ZONE] Mission managers missing on " + name
            );
            return;
        }

        if (!dayManager.IsSpawned ||
            !missionManager.IsSpawned ||
            dayManager.CurrentDay.Value != 1)
            return;

        localPlayerHasEntered = true;

        Debug.Log(
            "[OBJECTIVE ZONE] Player entered " + sectionName
        );

        // Automatically find the local HUD if necessary.
        if (missionUI == null)
            missionUI = FindFirstObjectByType<DayOneMissionUI>();

        if (missionUI != null)
        {
            missionUI.ShowSectionMessage(
                sectionName,
                sectionDescription
            );
        }
        else
        {
            Debug.LogError(
                "[OBJECTIVE ZONE] Could not find DayOneMissionUI!"
            );
        }

        // Report exploration to the networked mission manager.
        missionManager.ReportObjectiveCompleted(objectiveIndex);
    }
}
