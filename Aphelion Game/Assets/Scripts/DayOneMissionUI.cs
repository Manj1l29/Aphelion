
using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;

public class DayOneMissionUI : MonoBehaviour
{
    [Header("Managers")]
    [SerializeField] private MissionDayManager dayManager;
    [SerializeField] private DayOneMissionManager missionManager;

    [Header("Main HUD")]
    [SerializeField] private TMP_Text dayText;
    [SerializeField] private TMP_Text missionTitleText;
    [SerializeField] private TMP_Text progressText;
    [SerializeField] private TMP_Text returnPromptText;
    [SerializeField] private TMP_Text countdownText;

    [Header("Section Popup")]
    [SerializeField] private GameObject sectionPopup;
    [SerializeField] private TMP_Text sectionNameText;
    [SerializeField] private TMP_Text sectionDescriptionText;

    [SerializeField] private float popupDuration = 4f;

    private Coroutine popupCoroutine;

    private readonly string[] objectiveNames =
    {
        "COMMUNICATIONS",
        "SOLAR PANELS",
        "POWER HOUSE",
        "HABITAT"
    };

    private void Awake()
    {
        FindReferences();

        if (sectionPopup != null)
        {
            sectionPopup.SetActive(false);
        }

        if (dayText != null)
        {
            dayText.richText = true;
        }
    }

    private void FindReferences()
    {
        // Find managers automatically if not assigned.
        if (dayManager == null)
            dayManager = FindFirstObjectByType<MissionDayManager>();

        if (missionManager == null)
            missionManager = FindFirstObjectByType<DayOneMissionManager>();

        // Find UI elements by their GameObject names.
        if (sectionPopup == null)
            sectionPopup = FindChild("SectionPopup");

        if (sectionNameText == null)
            sectionNameText = FindText("SectionNameText");

        if (sectionDescriptionText == null)
            sectionDescriptionText = FindText("SectionDescriptionText");

        if (dayText == null)
            dayText = FindText("DayText");

        if (missionTitleText == null)
            missionTitleText = FindText("MissionTitleText");

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
        foreach (TMP_Text textObject in GetComponentsInChildren<TMP_Text>(true))
        {
            if (textObject.name == objectName)
                return textObject;
        }

        return null;
    }

    private void Update()
    {
        if (dayManager == null || !dayManager.IsSpawned)
            return;

        int currentDay = dayManager.CurrentDay.Value;

        if (dayText != null)
        {
            dayText.text =
                "DAY " + currentDay +
                " <size=60%><color=#9A8BC0>of 14</color></size>";
        }

        if (currentDay == 1)
        {
            if (missionManager == null || !missionManager.IsSpawned)
                return;

            UpdateDayOneHUD();
        }
        else
        {
            UpdateLaterDayHUD(currentDay);
        }
    }

    private void UpdateDayOneHUD()
    {
        if (missionTitleText != null)
            missionTitleText.text = "CREW ORIENTATION";

        if (progressText != null)
            progressText.text = BuildProgressText();

        bool allComplete = missionManager.AllObjectivesComplete;

        if (returnPromptText != null)
        {
            returnPromptText.gameObject.SetActive(allComplete);

            if (allComplete)
            {
                returnPromptText.text =
                    "ALL SECTIONS EXPLORED\nRETURN TO BASE";
            }
        }

        if (countdownText != null)
        {
            int countdown = missionManager.BaseCountdown.Value;

            countdownText.gameObject.SetActive(countdown > 0);

            if (countdown > 0)
            {
                countdownText.text =
                    "ALL CREW IN BASE\nDAY 2 BEGINS IN " +
                    countdown + "...";
            }
        }
    }

    private void UpdateLaterDayHUD(int currentDay)
    {
        if (missionTitleText != null)
            missionTitleText.text = "SURVIVAL OPERATIONS";

        if (progressText != null)
        {
            progressText.text =
                "DAY 1 COMPLETE\nALL FOUR SECTIONS EXPLORED";
        }

        if (returnPromptText != null)
        {
            returnPromptText.gameObject.SetActive(true);

            returnPromptText.text =
                "DAY " + currentDay +
                " HAS BEGUN\nNEW OBJECTIVES COMING SOON";
        }

        if (countdownText != null)
            countdownText.gameObject.SetActive(false);
    }

    private string BuildProgressText()
    {
        StringBuilder builder = new StringBuilder();

        builder.AppendLine(
            "SECTIONS EXPLORED: " +
            missionManager.CompletedCount + "/4"
        );

        builder.AppendLine();

        for (int i = 0; i < objectiveNames.Length; i++)
        {
            bool completed = missionManager.IsObjectiveComplete(i);

            string marker = completed ? "[X]" : "[ ]";

            builder.AppendLine(marker + " " + objectiveNames[i]);
        }

        return builder.ToString();
    }

    public void ShowSectionMessage(
        string sectionName,
        string description)
    {
        // Try finding missing UI references again.
        FindReferences();

        if (sectionPopup == null)
        {
            Debug.LogError(
                "[MISSION UI] SectionPopup was not found!"
            );
            return;
        }

        if (sectionNameText == null)
        {
            Debug.LogError(
                "[MISSION UI] SectionNameText was not found!"
            );
            return;
        }

        if (sectionDescriptionText == null)
        {
            Debug.LogError(
                "[MISSION UI] SectionDescriptionText was not found!"
            );
            return;
        }

        sectionNameText.text = sectionName;
        sectionDescriptionText.text = description;

        sectionPopup.SetActive(true);

        Debug.Log(
            "[MISSION UI] Showing section: " + sectionName
        );

        if (popupCoroutine != null)
        {
            StopCoroutine(popupCoroutine);
        }

        popupCoroutine = StartCoroutine(HideSectionPopup());
    }

    private IEnumerator HideSectionPopup()
    {
        yield return new WaitForSecondsRealtime(popupDuration);

        if (sectionPopup != null)
            sectionPopup.SetActive(false);

        popupCoroutine = null;
    }

    private void OnDisable()
    {
        if (popupCoroutine != null)
        {
            StopCoroutine(popupCoroutine);
            popupCoroutine = null;
        }
    }
}
