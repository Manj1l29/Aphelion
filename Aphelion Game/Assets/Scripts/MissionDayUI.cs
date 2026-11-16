
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MissionDayUI : MonoBehaviour
{
    [Header("Mission Manager")]
    [SerializeField] private MissionDayManager dayManager;

    [Header("Day Display")]
    [SerializeField] private TMP_Text dayText;

    [Header("Five Day Pips")]
    [SerializeField] private Image[] dayPips = new Image[5];

    [Header("Pip Colors")]
    [SerializeField] private Color completedColor =
        new Color(0.60f, 0.50f, 0.85f, 1f);

    [SerializeField] private Color currentColor =
        new Color(0.78f, 0.66f, 1f, 1f);

    [SerializeField] private Color upcomingColor =
        new Color(0.20f, 0.22f, 0.30f, 1f);

    private void Awake()
    {
        if (dayText != null)
            dayText.richText = true;

        FindDayManager();
        FindPips();
    }

    private void Update()
    {
        FindDayManager();

        if (dayManager == null || !dayManager.IsSpawned)
            return;

        UpdateDayDisplay();
        UpdateDayPips();
    }

    private void FindDayManager()
    {
        if (dayManager == null)
            dayManager =
                FindFirstObjectByType<MissionDayManager>();
    }

    private void FindPips()
    {
        if (dayPips == null || dayPips.Length != 5)
            dayPips = new Image[5];

        for (int i = 0; i < 5; i++)
        {
            if (dayPips[i] != null)
                continue;

            Transform pip = FindChild("DayPip" + (i + 1));

            if (pip != null)
                dayPips[i] = pip.GetComponent<Image>();
        }
    }

    private Transform FindChild(string objectName)
    {
        foreach (Transform child in
                 GetComponentsInChildren<Transform>(true))
        {
            if (child.name == objectName)
                return child;
        }

        return null;
    }

    private void UpdateDayDisplay()
    {
        if (dayText == null)
            return;

        int currentDay = dayManager.CurrentDay.Value;

        dayText.text =
            "DAY " + currentDay +
            " <size=60%><color=#9A8BC0>of " +
            MissionDayManager.TotalDays +
            "</color></size>";
    }

    private void UpdateDayPips()
    {
        int currentDay = dayManager.CurrentDay.Value;

        for (int i = 0; i < dayPips.Length; i++)
        {
            if (dayPips[i] == null)
                continue;

            if (i < currentDay - 1)
                dayPips[i].color = completedColor;
            else if (i == currentDay - 1)
                dayPips[i].color = currentColor;
            else
                dayPips[i].color = upcomingColor;
        }
    }
}
