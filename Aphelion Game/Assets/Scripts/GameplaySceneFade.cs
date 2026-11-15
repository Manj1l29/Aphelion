
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class GameplaySceneFade : MonoBehaviour
{
    [Header("Fade Settings")]
    [SerializeField] private Image fadePanel;
    [SerializeField] private float fadeDuration = 1.5f;

    private IEnumerator Start()
    {
        if (fadePanel == null)
        {
            Debug.LogError("[FADE IN] Panel Image is not assigned!");
            yield break;
        }

        // Start fully black.
        fadePanel.gameObject.SetActive(true);

        Color panelColor = fadePanel.color;
        panelColor.a = 1f;
        fadePanel.color = panelColor;

        Debug.Log("[FADE IN] Starting.");

        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            panelColor.a = Mathf.Lerp(
                1f,
                0f,
                Mathf.Clamp01(elapsed / fadeDuration)
            );

            fadePanel.color = panelColor;

            yield return null;
        }

        // Finish completely transparent.
        panelColor.a = 0f;
        fadePanel.color = panelColor;

        fadePanel.gameObject.SetActive(false);

        Debug.Log("[FADE IN] Complete.");
    }
}
