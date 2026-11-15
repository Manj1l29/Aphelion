
using System.Collections;
using UnityEngine;

public class GameplaySceneFade : MonoBehaviour
{
    [Header("Fade Settings")]
    public CanvasGroup fadePanel;
    public float fadeDuration = 1.5f;

    private IEnumerator Start()
    {
        if (fadePanel == null)
        {
            Debug.LogError("[FADE IN] Fade Panel is not assigned!");
            yield break;
        }

        // Start completely black.
        fadePanel.gameObject.SetActive(true);
        fadePanel.alpha = 1f;
        fadePanel.interactable = false;
        fadePanel.blocksRaycasts = true;

        Debug.Log("[FADE IN] Starting gameplay fade.");

        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            fadePanel.alpha = Mathf.Lerp(
                1f,
                0f,
                Mathf.Clamp01(elapsed / fadeDuration)
            );

            yield return null;
        }

        // Fully reveal the game.
        fadePanel.alpha = 0f;
        fadePanel.blocksRaycasts = false;
        fadePanel.gameObject.SetActive(false);

        Debug.Log("[FADE IN] Gameplay visible.");
    }
}
