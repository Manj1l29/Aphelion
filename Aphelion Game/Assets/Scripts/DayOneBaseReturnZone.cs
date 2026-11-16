
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class DayOneBaseReturnZone : MonoBehaviour
{
    [Header("Mission Manager")]
    [SerializeField] private DayOneMissionManager missionManager;

    // Tracks multiple colliders belonging to the local player.
    private readonly HashSet<int> localPlayerColliders =
        new HashSet<int>();

    private void Reset()
    {
        GetComponent<BoxCollider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        NetworkObject player =
            other.GetComponentInParent<NetworkObject>();

        if (player == null || !player.IsOwner)
            return;

        if (missionManager == null || !missionManager.IsSpawned)
            return;

        bool wasOutside = localPlayerColliders.Count == 0;

        localPlayerColliders.Add(other.GetInstanceID());

        // Only report the first entry, not every child collider.
        if (wasOutside && localPlayerColliders.Count > 0)
        {
            Debug.Log("[BASE ZONE] Local player entered base.");

            missionManager.ReportBasePresence(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        NetworkObject player =
            other.GetComponentInParent<NetworkObject>();

        if (player == null || !player.IsOwner)
            return;

        bool wasInside = localPlayerColliders.Count > 0;

        localPlayerColliders.Remove(other.GetInstanceID());

        if (wasInside && localPlayerColliders.Count == 0)
        {
            Debug.Log("[BASE ZONE] Local player left base.");

            missionManager.ReportBasePresence(false);
        }
    }

    private void OnDisable()
    {
        if (localPlayerColliders.Count > 0 &&
            missionManager != null &&
            missionManager.IsSpawned)
        {
            missionManager.ReportBasePresence(false);
        }

        localPlayerColliders.Clear();
    }
}
