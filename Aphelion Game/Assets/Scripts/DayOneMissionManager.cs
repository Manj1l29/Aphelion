
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class DayOneMissionManager : NetworkBehaviour
{
    public const int Communications = 0;
    public const int SolarPanels = 1;
    public const int PowerHouse = 2;
    public const int Habitat = 3;
    public const int ObjectiveCount = 4;

    [Header("Day Manager")]
    [SerializeField] private MissionDayManager dayManager;

    [Header("Networked Mission State")]
    public NetworkList<int> CompletedObjectives =
        new NetworkList<int>();

    public NetworkVariable<int> CurrentObjectiveIndex =
        new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    public NetworkVariable<int> BaseCountdown =
        new NetworkVariable<int>(
            -1,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private readonly HashSet<ulong> playersInBase =
        new HashSet<ulong>();

    private Coroutine returnCountdownRoutine;

    public int CompletedCount => CompletedObjectives.Count;

    public bool AllObjectivesComplete =>
        CurrentObjectiveIndex.Value >= ObjectiveCount;

    private bool IsDayOne =>
        dayManager != null &&
        dayManager.IsSpawned &&
        dayManager.CurrentDay.Value == 1;

    public bool IsObjectiveComplete(int objectiveId)
    {
        return CompletedObjectives.Contains(objectiveId);
    }

    public override void OnNetworkSpawn()
    {
        if (dayManager == null)
            dayManager = FindFirstObjectByType<MissionDayManager>();

        if (dayManager == null)
            Debug.LogError("[MISSION] MissionDayManager missing!");

        if (IsServer && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback +=
                HandleClientConnected;

            NetworkManager.Singleton.OnClientDisconnectCallback +=
                HandleClientDisconnected;
        }

        Debug.Log("[MISSION] Day 1 manager spawned.");
    }

    public override void OnNetworkDespawn()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -=
                HandleClientConnected;

            NetworkManager.Singleton.OnClientDisconnectCallback -=
                HandleClientDisconnected;
        }

        if (returnCountdownRoutine != null)
        {
            StopCoroutine(returnCountdownRoutine);
            returnCountdownRoutine = null;
        }
    }

    public void ReportObjectiveCompleted(int objectiveId)
    {
        if (!IsSpawned)
            return;

        CompleteObjectiveRpc(objectiveId);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void CompleteObjectiveRpc(
        int objectiveId,
        RpcParams rpcParams = default)
    {
        if (!IsServer || !IsDayOne)
            return;

        if (objectiveId < 0 || objectiveId >= ObjectiveCount)
            return;

        ulong senderId = rpcParams.Receive.SenderClientId;

        if (!NetworkManager.Singleton.ConnectedClients.ContainsKey(senderId))
            return;

        // Only the current objective can be completed.
        if (objectiveId != CurrentObjectiveIndex.Value)
            return;

        if (CompletedObjectives.Contains(objectiveId))
            return;

        CompletedObjectives.Add(objectiveId);

        CurrentObjectiveIndex.Value++;

        Debug.Log(
            "[MISSION] Completed objective " +
            objectiveId + ". Progress: " +
            CompletedCount + "/" + ObjectiveCount
        );

        if (AllObjectivesComplete)
        {
            Debug.Log("[MISSION] ALL SECTIONS COMPLETE. RETURN TO BASE!");
        }

        TryStartReturnCountdown();
    }

    public void ReportBasePresence(bool isInside)
    {
        if (!IsSpawned)
            return;

        UpdateBasePresenceRpc(isInside);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void UpdateBasePresenceRpc(
        bool isInside,
        RpcParams rpcParams = default)
    {
        if (!IsServer || !IsDayOne)
            return;

        ulong senderId = rpcParams.Receive.SenderClientId;

        if (!NetworkManager.Singleton.ConnectedClients.ContainsKey(senderId))
            return;

        if (isInside)
        {
            playersInBase.Add(senderId);
            Debug.Log("[MISSION] Client " + senderId + " entered base.");
        }
        else
        {
            playersInBase.Remove(senderId);
            Debug.Log("[MISSION] Client " + senderId + " left base.");

            CancelReturnCountdown();
        }

        TryStartReturnCountdown();
    }

    private void TryStartReturnCountdown()
    {
        if (!IsServer || !IsDayOne)
            return;

        if (!AllObjectivesComplete)
            return;

        if (returnCountdownRoutine != null)
            return;

        if (!AreAllConnectedPlayersInBase())
            return;

        Debug.Log("[MISSION] All crew returned. Starting countdown.");

        returnCountdownRoutine = StartCoroutine(ReturnCountdown());
    }

    private bool AreAllConnectedPlayersInBase()
    {
        NetworkManager nm = NetworkManager.Singleton;

        if (nm == null || nm.ConnectedClientsList.Count == 0)
            return false;

        foreach (NetworkClient client in nm.ConnectedClientsList)
        {
            if (!playersInBase.Contains(client.ClientId))
                return false;
        }

        return true;
    }

    private IEnumerator ReturnCountdown()
    {
        for (int seconds = 3; seconds >= 1; seconds--)
        {
            if (!AllObjectivesComplete ||
                !AreAllConnectedPlayersInBase() ||
                !IsDayOne)
            {
                BaseCountdown.Value = -1;
                returnCountdownRoutine = null;
                yield break;
            }

            BaseCountdown.Value = seconds;

            Debug.Log("[MISSION] Day transition in " + seconds);

            yield return new WaitForSecondsRealtime(1f);
        }

        if (AllObjectivesComplete &&
            AreAllConnectedPlayersInBase() &&
            IsDayOne)
        {
            bool started = dayManager != null &&
                           dayManager.AdvanceDayServer();

            BaseCountdown.Value = started ? 0 : -1;
        }
        else
        {
            BaseCountdown.Value = -1;
        }

        returnCountdownRoutine = null;
    }

    private void CancelReturnCountdown()
    {
        if (returnCountdownRoutine != null)
        {
            StopCoroutine(returnCountdownRoutine);
            returnCountdownRoutine = null;
        }

        BaseCountdown.Value = -1;

        Debug.Log("[MISSION] Base countdown cancelled.");
    }

    private void HandleClientConnected(ulong clientId)
    {
        playersInBase.Remove(clientId);

        if (returnCountdownRoutine != null)
            CancelReturnCountdown();
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        playersInBase.Remove(clientId);

        if (returnCountdownRoutine != null &&
            !AreAllConnectedPlayersInBase())
        {
            CancelReturnCountdown();
        }

        TryStartReturnCountdown();
    }
}
