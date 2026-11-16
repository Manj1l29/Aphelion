
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

public class SurvivalHUD : MonoBehaviour
{
    [Header("Survival Manager")]
    [SerializeField] private SurvivalSystemsManager survivalManager;

    [Header("Oxygen")]
    [SerializeField] private TMP_Text oxygenText;
    [SerializeField] private Image oxygenFill;

    [Header("Radiation Exposure")]
    [SerializeField] private TMP_Text radiationText;
    [SerializeField] private Image radiationFill;

    [Header("Data Source Display")]
    [SerializeField] private TMP_Text dataSourceText;

    private void Update()
    {
        FindSurvivalManager();

        if (survivalManager == null ||
            !survivalManager.IsSpawned ||
            !survivalManager.NasaDataReady.Value)
            return;

        UpdateOxygen();
        UpdateRadiation();
        UpdateDataSource();
    }

    private void FindSurvivalManager()
    {
        if (survivalManager == null)
        {
            survivalManager =
                FindFirstObjectByType<SurvivalSystemsManager>();
        }
    }

    private void UpdateOxygen()
    {
        float oxygen =
            survivalManager.OxygenPercent.Value;

        if (oxygenText != null)
        {
            oxygenText.text =
                "OXYGEN  " + oxygen.ToString("F0") + "%";
        }

        if (oxygenFill != null)
        {
            oxygenFill.fillAmount = oxygen / 100f;
        }
    }

    private void UpdateRadiation()
    {
        float exposure =
            survivalManager.RadiationExposurePercent.Value;

        if (radiationText != null)
        {
            radiationText.text =
                "RADIATION EXPOSURE  " +
                exposure.ToString("F1") + "%";
        }

        if (radiationFill != null)
        {
            radiationFill.fillAmount = exposure / 100f;
        }
    }

    private void UpdateDataSource()
    {
        if (dataSourceText == null)
            return;

        bool radLabAvailable =
            survivalManager.RadLabDataAvailable.Value;

        bool donkiAvailable =
            survivalManager.DonkiDataAvailable.Value;

        string radiationSource = radLabAvailable
            ? "NASA RADLAB BLEO REFERENCE"
            : "SIMULATED RADIATION FALLBACK";

        string weatherSource = donkiAvailable
            ? "DONKI SEP CONNECTED"
            : "DONKI UNAVAILABLE";

        dataSourceText.text =
            radiationSource + "\n" + weatherSource +
            "\nOXYGEN: NASA-INFORMED SIMULATION";
    }
}
