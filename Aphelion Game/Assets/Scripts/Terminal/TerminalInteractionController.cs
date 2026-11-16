
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;

public class TerminalInteractionController : MonoBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] private float interactionDistance = 5f;
    [SerializeField] private LayerMask interactionLayers = ~0;

    [Header("Interaction UI")]
    [SerializeField] private GameObject interactionPrompt;
    [SerializeField] private TMP_Text interactionText;
    [SerializeField] private Slider holdSlider;

    [Header("Terminal UI")]
    [SerializeField] private GameObject terminalPanel;
    [SerializeField] private Button exitButton;

    [Header("Gameplay HUD To Hide")]
    [SerializeField] private GameObject[] hudElementsToHide;

    [Header("Crosshair")]
    [SerializeField] private GameObject crosshair;

    private PlayerMovement playerMovement;
    private Transform playerCamera;
    private NetworkObject localPlayerNetworkObject;
    private TerminalInteractable currentTerminal;

    private float holdTimer;
    private bool terminalOpen;

    // Saves the original active state of every HUD element.
    private bool[] previousHudStates;
    private bool[] managedHudEntries;

    private bool previousCrosshairState;
    private bool hudStatesSaved;

    private void Awake()
    {
        if (interactionPrompt != null)
            interactionPrompt.SetActive(false);

        if (terminalPanel != null)
            terminalPanel.SetActive(false);

        if (holdSlider != null)
        {
            holdSlider.minValue = 0f;
            holdSlider.maxValue = 1f;
            holdSlider.value = 0f;
        }

        if (interactionText != null)
            interactionText.text = "HOLD E TO ACTION";

        ConnectExitButton();

        Debug.Log("[TERMINAL] Controller initialized.");
    }

    private void ConnectExitButton()
    {
        if (exitButton == null && terminalPanel != null)
        {
            Button[] buttons =
                terminalPanel.GetComponentsInChildren<Button>(true);

            foreach (Button button in buttons)
            {
                string buttonName =
                    button.gameObject.name.ToLowerInvariant();

                if (buttonName.Contains("exit") ||
                    buttonName.Contains("close"))
                {
                    exitButton = button;
                    break;
                }
            }
        }

        if (exitButton != null)
        {
            exitButton.onClick.RemoveListener(CloseTerminal);
            exitButton.onClick.AddListener(CloseTerminal);

            Debug.Log("[TERMINAL] Exit button connected.");
        }
        else
        {
            Debug.LogWarning(
                "[TERMINAL] Exit button not assigned or found."
            );
        }
    }

    private void Update()
    {
        FindLocalPlayer();

        if (terminalOpen)
        {
            HandleTerminalUI();
            return;
        }

        if (playerMovement == null || playerCamera == null)
            return;

        if (Cursor.lockState != CursorLockMode.Locked)
        {
            ResetInteraction();
            return;
        }

        DetectTerminal();
        HandleInteraction();
    }

    private void FindLocalPlayer()
    {
        if (playerMovement != null &&
            (!playerMovement.IsOwner ||
             !playerMovement.isActiveAndEnabled))
        {
            playerMovement = null;
            playerCamera = null;
            localPlayerNetworkObject = null;
        }

        if (playerMovement == null)
        {
            PlayerMovement[] players =
                FindObjectsByType<PlayerMovement>(
                    FindObjectsSortMode.None
                );

            foreach (PlayerMovement player in players)
            {
                if (player == null ||
                    !player.IsOwner ||
                    !player.isActiveAndEnabled ||
                    player.cam == null)
                    continue;

                playerMovement = player;
                playerCamera = player.cam;

                localPlayerNetworkObject =
                    player.GetComponent<NetworkObject>();

                break;
            }
        }
    }

    private void DetectTerminal()
    {
        if (playerCamera == null)
            return;

        TerminalInteractable foundTerminal = null;

        Ray ray = new Ray(
            playerCamera.position,
            playerCamera.forward
        );

        RaycastHit[] hits = Physics.RaycastAll(
            ray,
            interactionDistance,
            interactionLayers,
            QueryTriggerInteraction.Ignore
        );

        Array.Sort(
            hits,
            (a, b) => a.distance.CompareTo(b.distance)
        );

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null)
                continue;

            NetworkObject hitNetworkObject =
                hit.collider.GetComponentInParent<NetworkObject>();

            if (localPlayerNetworkObject != null &&
                hitNetworkObject == localPlayerNetworkObject)
                continue;

            foundTerminal =
                hit.collider.GetComponentInParent<TerminalInteractable>();

            if (foundTerminal == null)
                break;

            break;
        }

        if (foundTerminal != currentTerminal)
        {
            currentTerminal = foundTerminal;
            holdTimer = 0f;
            UpdateSlider();
        }

        bool detected = currentTerminal != null;

        if (interactionPrompt != null)
            interactionPrompt.SetActive(detected);

        if (detected && interactionText != null)
            interactionText.text = "HOLD E TO ACTION";
    }

    private void HandleInteraction()
    {
        if (currentTerminal == null ||
            Keyboard.current == null)
            return;

        if (Keyboard.current.eKey.isPressed)
        {
            holdTimer += Time.unscaledDeltaTime;

            float duration =
                Mathf.Max(0.01f, currentTerminal.holdDuration);

            if (holdSlider != null)
            {
                holdSlider.value =
                    Mathf.Clamp01(holdTimer / duration);
            }

            if (holdTimer >= duration)
                OpenTerminal();
        }
        else
        {
            holdTimer = 0f;
            UpdateSlider();
        }
    }

    private void ResetInteraction()
    {
        currentTerminal = null;
        holdTimer = 0f;

        if (interactionPrompt != null)
            interactionPrompt.SetActive(false);

        UpdateSlider();
    }

    private void UpdateSlider()
    {
        if (holdSlider != null)
            holdSlider.value = 0f;
    }

    private void OpenTerminal()
    {
        if (terminalPanel == null || terminalOpen)
            return;

        FindLocalPlayer();

        if (playerMovement == null)
        {
            Debug.LogError("[TERMINAL] Local player not found!");
            return;
        }

        Debug.Log("[TERMINAL] Opening terminal.");

        // Save HUD states BEFORE hiding anything.
        SaveAndHideGameplayHUD();

        terminalOpen = true;
        currentTerminal = null;
        holdTimer = 0f;

        if (interactionPrompt != null)
            interactionPrompt.SetActive(false);

        UpdateSlider();

        if (crosshair != null)
        {
            previousCrosshairState = crosshair.activeSelf;
            crosshair.SetActive(false);
        }

        terminalPanel.SetActive(true);

        playerMovement.SetTerminalUIOpen(true);

        Debug.Log("[TERMINAL] Terminal opened successfully.");
    }

    private void SaveAndHideGameplayHUD()
    {
        if (hudStatesSaved)
            return;

        if (hudElementsToHide == null)
            return;

        previousHudStates =
            new bool[hudElementsToHide.Length];

        managedHudEntries =
            new bool[hudElementsToHide.Length];

        // Save ALL original states first.
        for (int i = 0; i < hudElementsToHide.Length; i++)
        {
            GameObject item = hudElementsToHide[i];

            if (item == null)
                continue;

            bool wouldHideController =
                item == gameObject ||
                transform.IsChildOf(item.transform);

            bool wouldHideTerminal =
                item == terminalPanel ||
                terminalPanel.transform.IsChildOf(item.transform);

            if (wouldHideController || wouldHideTerminal)
            {
                Debug.LogWarning(
                    "[TERMINAL] Skipping unsafe HUD entry: " +
                    item.name
                );

                continue;
            }

            previousHudStates[i] = item.activeSelf;
            managedHudEntries[i] = true;

            Debug.Log(
                "[TERMINAL] Saved " + item.name +
                " = " + previousHudStates[i]
            );
        }

        hudStatesSaved = true;

        // Only after all states are saved, hide the HUD.
        for (int i = 0; i < hudElementsToHide.Length; i++)
        {
            if (hudElementsToHide[i] == null ||
                !managedHudEntries[i])
                continue;

            hudElementsToHide[i].SetActive(false);
        }

        Debug.Log("[TERMINAL] Gameplay HUD hidden.");
    }

    private void RestoreGameplayHUD()
    {
        if (!hudStatesSaved)
        {
            Debug.LogWarning("[TERMINAL] No HUD states were saved.");
            return;
        }

        // Restore every element to its original state.
        for (int i = 0; i < hudElementsToHide.Length; i++)
        {
            if (hudElementsToHide[i] == null ||
                !managedHudEntries[i])
                continue;

            hudElementsToHide[i].SetActive(
                previousHudStates[i]
            );

            Debug.Log(
                "[TERMINAL] Restored " +
                hudElementsToHide[i].name +
                " = " + previousHudStates[i]
            );
        }

        hudStatesSaved = false;
        previousHudStates = null;
        managedHudEntries = null;

        if (crosshair != null)
            crosshair.SetActive(previousCrosshairState);

        Debug.Log("[TERMINAL] All HUD states restored.");
    }

    public void CloseTerminal()
    {
        Debug.Log("[TERMINAL] EXIT PRESSED!");

        // Close even if the internal flag got out of sync.
        terminalOpen = false;

        if (terminalPanel != null)
            terminalPanel.SetActive(false);

        if (interactionPrompt != null)
            interactionPrompt.SetActive(false);

        UpdateSlider();

        currentTerminal = null;
        holdTimer = 0f;

        // Restore all previous HUD states.
        RestoreGameplayHUD();

        FindLocalPlayer();

        if (playerMovement != null)
        {
            playerMovement.SetTerminalUIOpen(false);
        }

        Debug.Log(
            "[TERMINAL] CLOSED. HUD RESTORATION COMPLETE."
        );
    }

    private void HandleTerminalUI()
    {
        if (Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            CloseTerminal();
        }
    }

    private void OnDestroy()
    {
        if (exitButton != null)
            exitButton.onClick.RemoveListener(CloseTerminal);
    }
}
