
using UnityEngine;

public class TerminalUIClose : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TerminalInteractionController interactionController;

    [Header("Terminal UI Root")]
    [SerializeField] private GameObject terminalUIRoot;

    private void Awake()
    {
        if (terminalUIRoot == null)
            terminalUIRoot = gameObject;

        if (interactionController == null)
        {
            interactionController =
                FindFirstObjectByType<TerminalInteractionController>();
        }
    }

    public void CloseTerminal()
    {
        Debug.Log("[TERMINAL CLOSE] EXIT BUTTON PRESSED!");

        if (interactionController != null)
        {
            interactionController.CloseTerminal();
            Debug.Log("[TERMINAL CLOSE] Gameplay controls restored.");
        }
        else
        {
            Debug.LogError(
                "[TERMINAL CLOSE] Interaction Controller not found!"
            );
        }

        // Force the entire terminal UI parent off.
        if (terminalUIRoot != null)
        {
            terminalUIRoot.SetActive(false);

            Debug.Log(
                "[TERMINAL CLOSE] Terminal UI active: " +
                terminalUIRoot.activeSelf
            );
        }

        Debug.Log("[TERMINAL CLOSE] FINISHED.");
    }
}
