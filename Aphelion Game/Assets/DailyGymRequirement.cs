using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class DailyGymRequirement : NetworkBehaviour
{
    [SerializeField] private MissionDayManager dayManager;
    [SerializeField] private GameObject beacon;
    [SerializeField] private int firstDay = 1;
    [SerializeField] private int lastDay = 5;

    public NetworkVariable<bool> CompletedToday = new NetworkVariable<bool>(false);

    public bool IsDoneToday => CompletedToday.Value;

    private void Awake()
    {
        GetComponent<BoxCollider>().isTrigger = true;

        if (dayManager == null)
            dayManager = FindFirstObjectByType<MissionDayManager>();
    }

    public override void OnNetworkSpawn()
    {
        // Host only: reset the gym every time the day changes
        if (IsServer && dayManager != null)
            dayManager.CurrentDay.OnValueChanged += OnDayChanged;

        // Everyone: update the beacon when the value changes
        CompletedToday.OnValueChanged += (oldValue, newValue) => RefreshBeacon();
        RefreshBeacon();
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && dayManager != null)
            dayManager.CurrentDay.OnValueChanged -= OnDayChanged;
    }

    private void OnDayChanged(int oldDay, int newDay)
    {
        CompletedToday.Value = false;
    }

    private bool IsGymDay()
    {
        if (dayManager == null || !dayManager.IsSpawned) return false;

        int day = dayManager.CurrentDay.Value;
        return day >= firstDay && day <= lastDay;
    }

    private void OnTriggerEnter(Collider other)
    {
        NetworkObject player = other.GetComponentInParent<NetworkObject>();
        if (player == null || !player.IsOwner) return;   // only react to MY player

        if (!IsSpawned || CompletedToday.Value || !IsGymDay()) return;

        ReportGymDoneRpc();
    }

    [Rpc(SendTo.Server)]
    private void ReportGymDoneRpc()
    {
        if (!IsGymDay()) return;   // the host double-checks
        CompletedToday.Value = true;
        Debug.Log("[GYM] Workout complete for today");
    }

    private void Update()
    {
        RefreshBeacon();   // also catches the day changing
    }

    private void RefreshBeacon()
    {
        if (beacon == null) return;

        bool show = IsSpawned && IsGymDay() && !CompletedToday.Value;

        if (beacon.activeSelf != show)
            beacon.SetActive(show);
    }
}
