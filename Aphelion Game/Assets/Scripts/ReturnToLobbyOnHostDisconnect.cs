
using System.Collections;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ReturnToLobbyOnHostDisconnect : MonoBehaviour
{
    [Header("Scenes")]
    [SerializeField] private string gameplaySceneName = "MainScene";
    [SerializeField] private string lobbySceneName = "LobbyScene";

    private NetworkManager networkManager;

    private Coroutine returnRoutine;
    private bool isReturning;

    // The session reference is registered by NetworkLauncher.
    private static ISession activeSession;

    public static void RegisterSession(ISession session)
    {
        activeSession = session;

        Debug.Log("[DISCONNECT] Multiplayer session registered.");
    }

    public static void ClearSession()
    {
        activeSession = null;
    }

    private void Awake()
    {
        networkManager = GetComponent<NetworkManager>();

        if (networkManager == null)
            networkManager = NetworkManager.Singleton;

        if (networkManager == null)
        {
            Debug.LogError(
                "[DISCONNECT] NetworkManager not found!"
            );

            enabled = false;
            return;
        }

        networkManager.OnClientDisconnectCallback +=
            HandleClientDisconnect;

        networkManager.OnTransportFailure +=
            HandleTransportFailure;

        Debug.Log("[DISCONNECT] Host disconnect handler ready.");
    }

    private void HandleClientDisconnect(ulong clientId)
    {
        if (networkManager == null)
            return;

        // The server/host does not return because another client left.
        if (networkManager.IsServer)
            return;

        // On a client, this callback identifies the local client.
        if (clientId != networkManager.LocalClientId)
            return;

        Debug.Log(
            "[DISCONNECT] Connection lost. Returning to lobby."
        );

        ReturnToLobby();
    }

    private void HandleTransportFailure()
    {
        if (networkManager == null)
            return;

        // A transport failure also sends the local client back.
        if (!networkManager.IsClient)
            return;

        Debug.Log(
            "[DISCONNECT] Network transport failed. Returning to lobby."
        );

        ReturnToLobby();
    }

    private void ReturnToLobby()
    {
        if (isReturning)
            return;

        // Only apply this automatic behavior during gameplay.
        if (SceneManager.GetActiveScene().name != gameplaySceneName)
            return;

        isReturning = true;

        if (returnRoutine != null)
            StopCoroutine(returnRoutine);

        returnRoutine = StartCoroutine(ReturnToLobbyRoutine());
    }

    private IEnumerator ReturnToLobbyRoutine()
    {
        Debug.Log("[DISCONNECT] Cleaning up multiplayer session.");

        // Unlock the cursor.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Leave the Unity Multiplayer session if registered.
        ISession sessionToLeave = activeSession;
        activeSession = null;

        if (sessionToLeave != null)
        {
            Task leaveTask = null;

            try
            {
                leaveTask = sessionToLeave.LeaveAsync();
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning(
                    "[DISCONNECT] Session leave request failed: " +
                    exception.Message
                );
            }

            // Wait briefly for service cleanup.
            float elapsed = 0f;
            const float cleanupTimeout = 4f;

            while (leaveTask != null &&
                   !leaveTask.IsCompleted &&
                   elapsed < cleanupTimeout)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (leaveTask != null && leaveTask.IsFaulted)
            {
                Debug.LogWarning(
                    "[DISCONNECT] Session cleanup error: " +
                    leaveTask.Exception
                );
            }
            else if (leaveTask != null && leaveTask.IsCompletedSuccessfully)
            {
                Debug.Log("[DISCONNECT] Session cleanup completed.");
            }
        }
        else
        {
            Debug.LogWarning(
                "[DISCONNECT] No session reference registered. " +
                "Continuing with network shutdown."
            );
        }

        // Shut down any remaining local NGO connection.
        if (networkManager != null && networkManager.IsListening)
        {
            networkManager.Shutdown();
        }

        // Give Netcode time to finish shutting down.
        yield return null;
        yield return null;

        Debug.Log("[DISCONNECT] Loading LobbyScene.");

        // This is a LOCAL scene load, not a network scene load.
        SceneManager.LoadScene(
            lobbySceneName,
            LoadSceneMode.Single
        );
    }

    private void OnDestroy()
    {
        if (networkManager != null)
        {
            networkManager.OnClientDisconnectCallback -=
                HandleClientDisconnect;

            networkManager.OnTransportFailure -=
                HandleTransportFailure;
        }
    }
}
