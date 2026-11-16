
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

    [Header("Player")]
    [SerializeField] private PlayerMovement playerMovement;

    private Transform playerCamera;
    private NetworkObject localPlayerNetworkObject;
    private TerminalInteractable currentTerminal;

    private float holdTimer;
    private bool terminalOpen;

    private bool[] previousHudStates;
    private bool previousCrosshairState;
    private bool previousPromptState;

    private void Awake()
    {
        // Set up the prompt.
        if (interactionPrompt != null)
            interactionPrompt.SetActive(false);

        // Set up the slider.
        if (holdSlider != null)
        {
            holdSlider.minValue = 0f;
            holdSlider.maxValue = 1f;
            holdSlider.value = 0f;
        }

        if (interactionText != null)
            interactionText.text = "HOLD E TO ACTION";

        // Automatically find ExitButton if not assigned.
        if (exitButton == null && terminalPanel != null)
        {
            Button[] buttons =
                terminalPanel.GetComponentsInChildren<Button>(true);

            foreach (Button button in buttons)
            {
                if (button.gameObject.name.Contains("Exit"))
                {
                    exitButton = button;
                    break;
                }
            }
        }

        // Connect the exit button automatically.
        if (exitButton != null)
        {
            exitButton.onClick.RemoveListener(CloseTerminal);
            exitButton.onClick.AddListener(CloseTerminal);

            Debug.Log("[TERMINAL] Exit button connected.");
        }
        else
        {
            Debug.LogWarning("[TERMINAL] Exit button not found!");
        }

        // Terminal starts closed.
        if (terminalPanel != null)
            terminalPanel.SetActive(false);

        Debug.Log("[TERMINAL] Controller initialized.");
    }

    private void Update()
    {
        FindLocalPlayer();

        if (playerMovement == null || playerCamera == null)
            return;

        if (terminalOpen)
        {
            HandleTerminalUI();
            return;
        }

        if (Cursor.lockState != CursorLockMode.Locked)
        {
            ResetInteraction();
            return;
        }

        DetectTerminal();
        HandleInteractionInput();
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

    private void HandleInteractionInput()
    {
        if (currentTerminal == null ||
            Keyboard.current == null)
            return;

        if (Keyboard.current.eKey.isPressed)
        {
            holdTimer += Time.unscaledDeltaTime;

            float duration =
                Mathf.Max(0.01f, currentTerminal.holdDuration);

            float progress =
                Mathf.Clamp01(holdTimer / duration);

            if (holdSlider != null)
                holdSlider.value = progress;

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
        if (terminalPanel == null)
        {
            Debug.LogError("[TERMINAL] Terminal Panel is missing!");
            return;
        }

        if (playerMovement == null)
            return;

        terminalOpen = true;

        ResetInteraction();

        // Remember and hide gameplay HUD.
        HideGameplayHUD();

        if (crosshair != null)
        {
            previousCrosshairState = crosshair.activeSelf;
            crosshair.SetActive(false);
        }

        // Show terminal.
        terminalPanel.SetActive(true);

        // Unlock mouse and freeze player controls.
        playerMovement.SetTerminalUIOpen(true);

        Debug.Log("[TERMINAL] Terminal opened. Gameplay HUD hidden.");
    }

    private void HideGameplayHUD()
    {
        if (hudElementsToHide == null)
            return;

        previousHudStates = new bool[hudElementsToHide.Length];

        for (int i = 0; i < hudElementsToHide.Length; i++)
        {
            GameObject hud = hudElementsToHide[i];

            if (hud == null || hud == terminalPanel)
                continue;

            previousHudStates[i] = hud.activeSelf;

            hud.SetActive(false);
        }
    }

    private void RestoreGameplayHUD()
    {
        if (hudElementsToHide == null ||
            previousHudStates == null)
            return;

        for (int i = 0; i < hudElementsToHide.Length; i++)
        {
            if (hudElementsToHide[i] != null)
            {
                hudElementsToHide[i].SetActive(
                    previousHudStates[i]
                );
            }
        }

        // Restore the original crosshair state.
        if (crosshair != null)
            crosshair.SetActive(previousCrosshairState);
    }

    private void HandleTerminalUI()
    {
        if (Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            CloseTerminal();
        }
    }

    // Called automatically by the Exit button.
    public void CloseTerminal()
    {
        if (!terminalOpen)
        {
            Debug.Log("[TERMINAL] Already closed.");
            return;
        }

        terminalOpen = false;

        // Hide terminal interface.
        if (terminalPanel != null)
            terminalPanel.SetActive(false);

        // Restore normal HUD.
        RestoreGameplayHUD();

        if (interactionPrompt != null)
            interactionPrompt.SetActive(false);

        holdTimer = 0f;
        currentTerminal = null;

        UpdateSlider();

        // Restore first-person controls and lock mouse.
        if (playerMovement != null)
            playerMovement.SetTerminalUIOpen(false);

        Debug.Log("[TERMINAL] Closed. Gameplay HUD restored.");
    }

    private void OnDestroy()
    {
        if (exitButton != null)
            exitButton.onClick.RemoveListener(CloseTerminal);
    }

    private void OnDisable()
    {
        if (terminalOpen)
        {
            terminalOpen = false;

            if (terminalPanel != null)
                terminalPanel.SetActive(false);

            RestoreGameplayHUD();

            if (playerMovement != null)
                playerMovement.SetTerminalUIOpen(false);
        }
    }
}
