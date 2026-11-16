
using System.Collections.Generic;
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

    [Header("Existing Red Beacon")]
    [SerializeField] private GameObject redBeacon;

    private readonly HashSet<int> localPlayerColliders =
        new HashSet<int>();

    private bool localPlayerHasReported;
    private bool warnedMissingBeacon;

    private void Awake()
    {
        GetComponent<BoxCollider>().isTrigger = true;

        ResolveReferences();

        // Every beacon starts hidden.
        SetBeacon(false);
    }

    private void Start()
    {
        ResolveReferences();
        RefreshBeacon();
    }

    private void Update()
    {
        ResolveReferences();

        RefreshBeacon();
        TryCompleteCurrentObjective();
    }

    private void ResolveReferences()
    {
        if (dayManager == null)
            dayManager = FindFirstObjectByType<MissionDayManager>();

        if (missionManager == null)
            missionManager = FindFirstObjectByType<DayOneMissionManager>();
    }

    private void OnTriggerEnter(Collider other)
    {
        NetworkObject player =
            other.GetComponentInParent<NetworkObject>();

        if (player == null || !player.IsOwner)
            return;

        localPlayerColliders.Add(other.GetInstanceID());

        TryCompleteCurrentObjective();
    }

    private void OnTriggerExit(Collider other)
    {
        NetworkObject player =
            other.GetComponentInParent<NetworkObject>();

        if (player == null || !player.IsOwner)
            return;

        localPlayerColliders.Remove(other.GetInstanceID());
    }

    private void TryCompleteCurrentObjective()
    {
        if (localPlayerColliders.Count == 0)
            return;

        if (localPlayerHasReported)
            return;

        if (dayManager == null || missionManager == null)
            return;

        if (!dayManager.IsSpawned || !missionManager.IsSpawned)
            return;

        if (dayManager.CurrentDay.Value != 1)
            return;

        if (missionManager.CurrentObjectiveIndex.Value != objectiveIndex)
            return;

        localPlayerHasReported = true;

        Debug.Log(
            "[OBJECTIVE] Correct section entered: " + sectionName
        );

        missionManager.ReportObjectiveCompleted(objectiveIndex);
    }

    private void RefreshBeacon()
    {
        if (redBeacon == null)
        {
            if (!warnedMissingBeacon)
            {
                Debug.LogWarning(
                    "[BEACON] No beacon assigned to " + gameObject.name
                );

                warnedMissingBeacon = true;
            }

            return;
        }

        bool shouldShow = false;

        if (dayManager != null &&
            missionManager != null &&
            dayManager.IsSpawned &&
            missionManager.IsSpawned)
        {
            shouldShow =
                dayManager.CurrentDay.Value == 1 &&
                missionManager.CurrentObjectiveIndex.Value == objectiveIndex &&
                !missionManager.IsObjectiveComplete(objectiveIndex);
        }

        SetBeacon(shouldShow);
    }

    private void SetBeacon(bool visible)
    {
        if (redBeacon == null)
            return;

        if (redBeacon == gameObject)
        {
            Debug.LogError(
                "[BEACON] The beacon cannot be the objective zone itself!"
            );
            return;
        }

        if (redBeacon.activeSelf != visible)
        {
            redBeacon.SetActive(visible);

            Debug.Log(
                "[BEACON] " + redBeacon.name +
                (visible ? " ACTIVATED" : " DEACTIVATED")
            );
        }
    }

    private void OnDisable()
    {
        localPlayerColliders.Clear();

        SetBeacon(false);
    }
}
