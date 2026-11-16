using UnityEngine;

public class DayPanelSwitcher : MonoBehaviour
{
    [SerializeField] private MissionDayManager dayManager;

    [Tooltip("Element 0 = Day 1 panel, Element 1 = Day 2 panel, and so on")]
    [SerializeField] private GameObject[] dayPanels;

    private int shownDay = -1;

    private void Awake()
    {
        if (dayManager == null)
            dayManager = FindFirstObjectByType<MissionDayManager>();

        ShowDay(0);   // hide every panel until the game has started
    }

    private void Update()
    {
        if (dayManager == null || !dayManager.IsSpawned) return;

        int day = dayManager.CurrentDay.Value;

        if (day != shownDay)
            ShowDay(day);
    }

    private void ShowDay(int day)
    {
        shownDay = day;

        for (int i = 0; i < dayPanels.Length; i++)
        {
            if (dayPanels[i] != null)
                dayPanels[i].SetActive(i == day - 1);
        }
    }
}
