
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Networking;

public class SurvivalSystemsManager : NetworkBehaviour
{
    [Header("Mission Manager")]
    [SerializeField] private MissionDayManager dayManager;

    [Header("NASA API")]
    [SerializeField] private string nasaApiKey = "DEMO_KEY";
    [SerializeField] private int solarEventLookbackDays = 7;
    [SerializeField] private int radLabLookbackYears = 10;
    [SerializeField] private float apiTimeout = 20f;

    [Header("Oxygen Simulation")]
    [Tooltip("Oxygen percentage consumed per real minute on Day 2.")]
    [SerializeField] private float oxygenDrainPerMinute = 2.5f;

    [Tooltip("Additional consumption per minute for each later day.")]
    [SerializeField] private float oxygenDrainIncreasePerDay = 0.6f;

    [Tooltip("Expected full crew capacity.")]
    [SerializeField] private int plannedCrewCapacity = 4;

    [Header("Radiation Simulation")]
    [Tooltip("Used only if RadLab has no available BLEO data.")]
    [SerializeField] private float fallbackDoseRate = 0.5f;

    [Tooltip("Gameplay percentage added per simulated microSievert.")]
    [SerializeField] private float exposurePercentPerMicroSv = 0.15f;

    [Tooltip("Each real minute represents one simulated hour.")]
    [SerializeField] private float simulatedTimeCompression = 60f;

    [Tooltip("Radiation exposure growth multiplier per later day.")]
    [SerializeField] private float radiationIncreasePerDay = 0.2f;

    [Tooltip("Additional risk multiplier per recent SEP event.")]
    [SerializeField] private float sepEventBoostPerEvent = 0.2f;

    [SerializeField] private float maximumSepEventBoost = 1f;

    [Header("Networked Survival State")]
    public NetworkVariable<float> OxygenPercent =
        new NetworkVariable<float>(
            100f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    public NetworkVariable<float> RadiationExposurePercent =
        new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    public NetworkVariable<float> RadiationDoseEquivalentRate =
        new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    public NetworkVariable<int> RecentSEPEventCount =
        new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    public NetworkVariable<bool> NasaDataReady =
        new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    public NetworkVariable<bool> DonkiDataAvailable =
        new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    public NetworkVariable<bool> RadLabDataAvailable =
        new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private float simulationTimer;
    private Coroutine initializationRoutine;

    [Serializable]
    private class SepResponse
    {
        public SepEvent[] items;
    }

    [Serializable]
    private class SepEvent
    {
        public string sepID;
        public string eventTime;
    }

    public override void OnNetworkSpawn()
    {
        if (dayManager == null)
        {
            dayManager =
                FindFirstObjectByType<MissionDayManager>();
        }

        Debug.Log("[SURVIVAL] Survival manager spawned.");

        if (IsServer)
        {
            initializationRoutine =
                StartCoroutine(InitializeSurvivalSystems());
        }
    }

    public override void OnNetworkDespawn()
    {
        if (initializationRoutine != null)
        {
            StopCoroutine(initializationRoutine);
            initializationRoutine = null;
        }
    }

    private IEnumerator InitializeSurvivalSystems()
    {
        // Wait until MissionDayManager exists and is network spawned.
        float waitTime = 0f;

        while (dayManager == null && waitTime < 15f)
        {
            dayManager =
                FindFirstObjectByType<MissionDayManager>();

            waitTime += Time.unscaledDeltaTime;
            yield return null;
        }

        if (dayManager == null)
        {
            Debug.LogError(
                "[SURVIVAL] MissionDayManager could not be found!"
            );

            yield break;
        }

        while (!dayManager.IsSpawned)
            yield return null;

        Debug.Log("[SURVIVAL] Loading NASA data.");

        // Fetch space weather.
        yield return FetchDONKIData();

        // Fetch historical beyond-LEO radiation data.
        yield return FetchRadLabData();

        NasaDataReady.Value = true;

        Debug.Log(
            "[SURVIVAL] NASA data initialization complete."
        );

        Debug.Log(
            "[SURVIVAL] Recent SEP events: " +
            RecentSEPEventCount.Value
        );

        Debug.Log(
            "[SURVIVAL] Radiation reference rate: " +
            RadiationDoseEquivalentRate.Value +
            " microSv/hour"
        );
    }

    private IEnumerator FetchDONKIData()
    {
        string endDate =
            DateTime.UtcNow.ToString("yyyy-MM-dd");

        string startDate =
            DateTime.UtcNow
                .AddDays(-solarEventLookbackDays)
                .ToString("yyyy-MM-dd");

        string key = string.IsNullOrWhiteSpace(nasaApiKey)
            ? "DEMO_KEY"
            : nasaApiKey.Trim();

        string url =
            "https://api.nasa.gov/DONKI/SEP" +
            "?startDate=" + startDate +
            "&endDate=" + endDate +
            "&api_key=" + UnityWebRequest.EscapeURL(key);

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = Mathf.CeilToInt(apiTimeout);

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                DonkiDataAvailable.Value = false;

                Debug.LogWarning(
                    "[NASA DONKI] Request failed: " +
                    request.error
                );

                yield break;
            }

            try
            {
                string json = request.downloadHandler.text;

                // JsonUtility needs an object wrapper around a root array.
                string wrappedJson = "{\"items\":" + json + "}";

                SepResponse response =
                    JsonUtility.FromJson<SepResponse>(wrappedJson);

                if (response == null || response.items == null)
                {
                    DonkiDataAvailable.Value = false;

                    Debug.LogWarning(
                        "[NASA DONKI] Could not read SEP response."
                    );

                    yield break;
                }

                RecentSEPEventCount.Value =
                    response.items.Length;

                DonkiDataAvailable.Value = true;

                Debug.Log(
                    "[NASA DONKI] SEP events found: " +
                    RecentSEPEventCount.Value
                );
            }
            catch (Exception exception)
            {
                DonkiDataAvailable.Value = false;

                Debug.LogWarning(
                    "[NASA DONKI] JSON parsing failed: " +
                    exception.Message
                );
            }
        }
    }

    private IEnumerator FetchRadLabData()
    {
        string startDate =
            DateTime.UtcNow
                .AddYears(-radLabLookbackYears)
                .ToString("yyyy-MM-dd");

        string endDate =
            DateTime.UtcNow
                .AddDays(1)
                .ToString("yyyy-MM-dd");

        // Request historical BLEO dose-equivalent-rate measurements.
        string url =
            "https://visualization.osdr.nasa.gov/radlab/api/" +
            "?trajectory=BLEO" +
            "&timestamp%3E=" + startDate +
            "&timestamp%3C=" + endDate +
            "&dose_equivalent_rate" +
            "&format=csv";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = Mathf.CeilToInt(apiTimeout);

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                UseFallbackRadiationRate(
                    "[RADLAB] Request failed: " + request.error
                );

                yield break;
            }

            float measuredRate;

            if (TryGetRadLabMedianDoseRate(
                request.downloadHandler.text,
                out measuredRate))
            {
                RadiationDoseEquivalentRate.Value = measuredRate;
                RadLabDataAvailable.Value = true;

                Debug.Log(
                    "[RADLAB] Historical BLEO median dose rate: " +
                    measuredRate.ToString("F4") +
                    " microSv/hour"
                );
            }
            else
            {
                UseFallbackRadiationRate(
                    "[RADLAB] No usable BLEO measurements returned."
                );
            }
        }
    }

    private bool TryGetRadLabMedianDoseRate(
        string csv,
        out float median)
    {
        median = 0f;

        if (string.IsNullOrWhiteSpace(csv))
            return false;

        string[] lines = csv.Split(
            new char[] { '\r', '\n' },
            StringSplitOptions.RemoveEmptyEntries
        );

        if (lines.Length < 2)
            return false;

        string[] headers = lines[0].Split(',');
        int doseColumn = -1;

        for (int i = 0; i < headers.Length; i++)
        {
            string header = headers[i]
                .Trim()
                .Trim('"');

            if (header.Equals(
                "dose_equivalent_rate",
                StringComparison.OrdinalIgnoreCase))
            {
                doseColumn = i;
                break;
            }
        }

        if (doseColumn < 0)
        {
            Debug.LogWarning(
                "[RADLAB] Dose equivalent rate column not found."
            );

            return false;
        }

        List<float> values = new List<float>();

        for (int i = 1; i < lines.Length; i++)
        {
            string[] columns = lines[i].Split(',');

            if (doseColumn >= columns.Length)
                continue;

            string value = columns[doseColumn]
                .Trim()
                .Trim('"');

            float parsed;

            if (float.TryParse(
                value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out parsed))
            {
                if (parsed >= 0f)
                    values.Add(parsed);
            }
        }

        if (values.Count == 0)
            return false;

        values.Sort();

        int middle = values.Count / 2;

        if (values.Count % 2 == 0)
        {
            median =
                (values[middle - 1] + values[middle]) / 2f;
        }
        else
        {
            median = values[middle];
        }

        return true;
    }

    private void UseFallbackRadiationRate(string reason)
    {
        RadLabDataAvailable.Value = false;

        RadiationDoseEquivalentRate.Value =
            fallbackDoseRate;

        Debug.LogWarning(
            reason +
            " Using configured gameplay fallback instead."
        );
    }

    private void Update()
    {
        if (!IsServer || !NasaDataReady.Value)
            return;

        if (dayManager == null || !dayManager.IsSpawned)
            return;

        int currentDay = dayManager.CurrentDay.Value;

        // Day 1 is the tutorial. Resource depletion starts on Day 2.
        if (currentDay < 2 ||
            currentDay > MissionDayManager.TotalDays)
            return;

        simulationTimer += Time.unscaledDeltaTime;

        if (simulationTimer >= 1f)
        {
            float elapsed = simulationTimer;
            simulationTimer = 0f;

            UpdateSurvivalResources(elapsed, currentDay);
        }
    }

    private void UpdateSurvivalResources(
        float elapsedSeconds,
        int currentDay)
    {
        // Scale oxygen consumption according to the connected crew.
        int crewCount = 1;

        if (NetworkManager.Singleton != null)
        {
            crewCount = Mathf.Max(
                1,
                NetworkManager.Singleton.ConnectedClientsList.Count
            );
        }

        float crewMultiplier = Mathf.Clamp(
            (float)crewCount / Mathf.Max(1, plannedCrewCapacity),
            0.25f,
            1.5f
        );

        // Oxygen depletion increases as the mission progresses.
        float dayOffset = currentDay - 2;

        float currentOxygenDrain =
            oxygenDrainPerMinute +
            dayOffset * oxygenDrainIncreasePerDay;

        float oxygenLoss =
            currentOxygenDrain *
            crewMultiplier *
            (elapsedSeconds / 60f);

        OxygenPercent.Value = Mathf.Clamp(
            OxygenPercent.Value - oxygenLoss,
            0f,
            100f
        );

        // SEP events increase the simulated event-risk multiplier.
        float sepMultiplier = 1f + Mathf.Min(
            maximumSepEventBoost,
            RecentSEPEventCount.Value * sepEventBoostPerEvent
        );

        // Radiation exposure also becomes more demanding each day.
        float radiationDayMultiplier =
            1f + dayOffset * radiationIncreasePerDay;

        // One real minute represents one simulated hour.
        float simulatedHours =
            (elapsedSeconds / 3600f) *
            simulatedTimeCompression;

        float simulatedDose =
            RadiationDoseEquivalentRate.Value *
            simulatedHours;

        float exposureGain =
            simulatedDose *
            exposurePercentPerMicroSv *
            sepMultiplier *
            radiationDayMultiplier;

        RadiationExposurePercent.Value = Mathf.Clamp(
            RadiationExposurePercent.Value + exposureGain,
            0f,
            100f
        );
    }
}
