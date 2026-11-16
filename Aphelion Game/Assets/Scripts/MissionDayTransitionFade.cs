
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class MissionDayTransitionFade : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MissionDayManager dayManager;
    [SerializeField] private Image fadePanel;

    [Header("Startup")]
    [SerializeField] private bool fadeInOnSceneStart = true;

    private Coroutine fadeRoutine;

    private int lastHandledState = -1;

    private void Awake()
    {
        if (fadePanel == null)
            fadePanel = GetComponent<Image>();

        if (fadePanel == null)
        {
            Debug.LogError("[DAY FADE] Panel Image is missing!");
            enabled = false;
            return;
        }

        fadePanel.gameObject.SetActive(true);

        SetAlpha(fadeInOnSceneStart ? 1f : 0f);

        fadePanel.raycastTarget = fadeInOnSceneStart;
    }

    private void Start()
    {
        FindDayManager();

        // Fade in when MainScene first launches.
        if (fadeInOnSceneStart)
        {
            BeginFade(0f);
        }
    }

    private void Update()
    {
        FindDayManager();

        if (dayManager == null || !dayManager.IsSpawned)
            return;

        // Continuously monitor the synchronized transition state.
        int currentState = dayManager.TransitionState.Value;

        if (currentState == lastHandledState)
            return;

        lastHandledState = currentState;

        if (currentState == 1)
        {
            Debug.Log("[DAY FADE] Fading out.");
            BeginFade(1f);
        }
        else if (currentState == 2)
        {
            Debug.Log("[DAY FADE] Fading back in.");
            BeginFade(0f);
        }
    }

    private void FindDayManager()
    {
        if (dayManager == null)
        {
            dayManager =
                FindFirstObjectByType<MissionDayManager>();
        }
    }

    private void BeginFade(float targetAlpha)
    {
        if (fadePanel == null)
            return;

        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);

        fadeRoutine = StartCoroutine(FadeRoutine(targetAlpha));
    }

    private IEnumerator FadeRoutine(float targetAlpha)
    {
        fadePanel.gameObject.SetActive(true);

        // Block clicks while the screen is transitioning.
        fadePanel.raycastTarget = true;

        float startingAlpha = fadePanel.color.a;
        float elapsed = 0f;

        while (elapsed < MissionDayManager.FadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float progress = Mathf.Clamp01(
                elapsed / MissionDayManager.FadeDuration
            );

            float alpha = Mathf.Lerp(
                startingAlpha,
                targetAlpha,
                progress
            );

            SetAlpha(alpha);

            yield return null;
        }

        SetAlpha(targetAlpha);

        // Stop blocking gameplay when the screen is visible.
        fadePanel.raycastTarget = targetAlpha > 0.99f;

        Debug.Log(
            "[DAY FADE] Fade finished. Alpha: " + targetAlpha
        );

        fadeRoutine = null;
    }

    private void SetAlpha(float alpha)
    {
        if (fadePanel == null)
            return;

        Color color = fadePanel.color;
        color.a = alpha;
        fadePanel.color = color;
    }

    private void OnDestroy()
    {
        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);
    }
}
