
using Unity.Netcode;
using UnityEngine;

public class MissionDayManager : NetworkBehaviour
{
    public const int TotalDays = 14;

    public NetworkVariable<int> CurrentDay =
        new NetworkVariable<int>(
            1,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    public override void OnNetworkSpawn()
    {
        Debug.Log(
            "[DAY MANAGER] Mission started. Day " +
            CurrentDay.Value + " of " + TotalDays
        );

        CurrentDay.OnValueChanged += OnDayChanged;
    }

    public override void OnNetworkDespawn()
    {
        CurrentDay.OnValueChanged -= OnDayChanged;
    }

    // Only the server is allowed to advance the mission.
    public bool AdvanceDayServer()
    {
        if (!IsServer)
        {
            Debug.LogWarning("[DAY MANAGER] Only the server can advance days.");
            return false;
        }

        if (CurrentDay.Value >= TotalDays)
        {
            Debug.Log("[DAY MANAGER] All 14 mission days are complete.");
            return false;
        }

        CurrentDay.Value++;

        Debug.Log(
            "[DAY MANAGER] ADVANCED TO DAY " +
            CurrentDay.Value + " OF " + TotalDays
        );

        return true;
    }

    private void OnDayChanged(int previousDay, int newDay)
    {
        Debug.Log(
            "[DAY MANAGER] Day changed: " +
            previousDay + " -> " + newDay
        );
    }
}
