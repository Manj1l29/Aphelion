
using System.Text;
using TMPro;
using Unity.Netcode;
using UnityEngine;

public class DayOneMissionUI : MonoBehaviour
{
    [Header("Managers")]
    [SerializeField] private MissionDayManager dayManager;
    [SerializeField] private DayOneMissionManager missionManager;

    [Header("Current Objective")]
    [SerializeField] private GameObject currentObjectivePanel;
    [SerializeField] private TMP_Text currentObjectiveTitleText;
    [SerializeField] private TMP_Text currentObjectiveDescriptionText;

    [Header("Mission Checklist")]
    [SerializeField] private GameObject progressPanel;
    [SerializeField] private TMP_Text progressHeaderText;
    [SerializeField] private TMP_Text progressText;

    [Header("Return To Base")]
    [SerializeField] private GameObject returnPromptPanel;
    [SerializeField] private TMP_Text returnPromptText;

    [Header("Countdown")]
    [SerializeField] private GameObject countdownPanel;
    [SerializeField] private TMP_Text countdownText;

    private readonly string[] objectiveNames =
    {
        "COMMUNICATIONS",
        "SOLAR PANELS",
        "POWER HOUSE",
        "HABITAT"
    };

    private readonly string[] objectiveTitles =
    {
        "Explore Communications",
        "Explore Solar Panels",
        "Explore Power House",
        "Explore Habitat"
    };

    private readonly string[] objectiveDescriptions =
    {
        "Locate and enter the communications section of the lunar base.",
        "Locate and enter the solar panel area to explore the base's power generation system.",
        "Locate and enter the power house to explore the base's electrical distribution system.",
        "Locate and enter the habitat to explore the crew's living quarters."
    };

    private void Awake()
    {
        ResolveReferences();

        // Only configure initial visibility here.
        // Do not force these panels on during Update.
        SetPanel(returnPromptPanel, false);
        SetPanel(countdownPanel, false);
    }

    private void Update()
    {
        ResolveReferences();

        if (dayManager == null || missionManager == null)
            return;

        if (!dayManager.IsSpawned || !missionManager.IsSpawned)
            return;

        if (dayManager.CurrentDay.Value == 1)
            UpdateDayOne();
        else
            UpdateLaterDay(dayManager.CurrentDay.Value);
    }

    private void ResolveReferences()
    {
        if (dayManager == null)
            dayManager = FindFirstObjectByType<MissionDayManager>();

        if (missionManager == null)
            missionManager = FindFirstObjectByType<DayOneMissionManager>();

        if (currentObjectivePanel == null)
            currentObjectivePanel = FindChild("CurrentObjectivePanel");

        if (progressPanel == null)
            progressPanel = FindChild("ProgressPanel");

        if (returnPromptPanel == null)
            returnPromptPanel = FindChild("ReturnPromptPanel");

        if (countdownPanel == null)
            countdownPanel = FindChild("CountdownPanel");

        if (currentObjectiveTitleText == null)
            currentObjectiveTitleText = FindText("CurrentObjectiveTitleText");

        if (currentObjectiveDescriptionText == null)
            currentObjectiveDescriptionText = FindText("CurrentObjectiveDescriptionText");

        if (progressHeaderText == null)
            progressHeaderText = FindText("ProgressHeaderText");

        if (progressText == null)
            progressText = FindText("ProgressText");

        if (returnPromptText == null)
            returnPromptText = FindText("ReturnPromptText");

        if (countdownText == null)
            countdownText = FindText("CountdownText");
    }

    private GameObject FindChild(string objectName)
    {
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child.name == objectName)
                return child.gameObject;
        }

        return null;
    }

    private TMP_Text FindText(string objectName)
    {
        foreach (TMP_Text item in GetComponentsInChildren<TMP_Text>(true))
        {
            if (item.name == objectName)
                return item;
        }

        return null;
    }

    private void SetPanel(GameObject panel, bool active)
    {
        if (panel != null && panel.activeSelf != active)
            panel.SetActive(active);
    }

    private void UpdateDayOne()
    {
        int currentIndex =
            missionManager.CurrentObjectiveIndex.Value;

        if (currentIndex < objectiveNames.Length)
        {
            if (currentObjectiveTitleText != null)
                currentObjectiveTitleText.text =
                    objectiveTitles[currentIndex];

            if (currentObjectiveDescriptionText != null)
                currentObjectiveDescriptionText.text =
                    objectiveDescriptions[currentIndex];
        }
        else
        {
            if (currentObjectiveTitleText != null)
                currentObjectiveTitleText.text = "RETURN TO BASE";

            if (currentObjectiveDescriptionText != null)
            {
                currentObjectiveDescriptionText.text =
                    "All four sections have been explored. Regroup with your crew inside the base.";
            }
        }

        if (progressHeaderText != null)
        {
            progressHeaderText.text =
                "MISSION PROGRESS | " +
                missionManager.CompletedCount + "/4";
        }

        if (progressText != null)
            progressText.text = BuildProgressText(currentIndex);

        bool allComplete = missionManager.AllObjectivesComplete;

        SetPanel(returnPromptPanel, allComplete);

        if (returnPromptText != null && allComplete)
        {
            returnPromptText.text =
                "ALL SECTIONS EXPLORED\nRETURN TO BASE";
        }

        int countdown = missionManager.BaseCountdown.Value;

        SetPanel(countdownPanel, countdown > 0);

        if (countdownText != null && countdown > 0)
        {
            countdownText.text =
                "ALL CREW IN BASE\nDAY TRANSITION IN " +
                countdown + "...";
        }
    }

    private string BuildProgressText(int currentIndex)
    {
        StringBuilder builder = new StringBuilder();

        for (int i = 0; i < objectiveNames.Length; i++)
        {
            string marker;

            if (missionManager.IsObjectiveComplete(i))
                marker = "[X]";
            else if (i == currentIndex)
                marker = "[>]";
            else
                marker = "[ ]";

            builder.AppendLine(marker + " " + objectiveNames[i]);
        }

        return builder.ToString();
    }

    private void UpdateLaterDay(int currentDay)
    {
        if (currentObjectiveTitleText != null)
            currentObjectiveTitleText.text = "SURVIVAL OPERATIONS";

        if (currentObjectiveDescriptionText != null)
        {
            currentObjectiveDescriptionText.text =
                "Day " + currentDay +
                " has begun. New survival objectives will be assigned.";
        }

        if (progressHeaderText != null)
            progressHeaderText.text = "DAY 1 COMPLETE | 4/4";

        if (progressText != null)
        {
            progressText.text =
                "[X] COMMUNICATIONS\n" +
                "[X] SOLAR PANELS\n" +
                "[X] POWER HOUSE\n" +
                "[X] HABITAT";
        }

        SetPanel(returnPromptPanel, false);
        SetPanel(countdownPanel, false);
    }
}
