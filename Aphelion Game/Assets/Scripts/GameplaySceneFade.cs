
using System.Collections;
using UnityEngine;

public class GameplaySceneFade : MonoBehaviour
{
    [SerializeField] private CanvasGroup fadePanel;
    [SerializeField] private float fadeDuration = 1.5f;

    private IEnumerator Start()
    {
        if (fadePanel == null)
        {
            Debug.LogError("[FADE IN] CanvasGroup is not assigned!");
            yield break;
        }

        // Begin fully black.
        fadePanel.gameObject.SetActive(true);
        fadePanel.alpha = 1f;
        fadePanel.interactable = false;
        fadePanel.blocksRaycasts = true;

        Debug.Log("[FADE IN] Starting.");

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

        fadePanel.alpha = 0f;
        fadePanel.blocksRaycasts = false;
        fadePanel.gameObject.SetActive(false);

        Debug.Log("[FADE IN] Complete.");
    }
}
