using System.Threading.Tasks;
using TMPro;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.UI;

public class NetworkLauncher : MonoBehaviour
{
    public const int PartySize = 4;

    [Header("Menu")]
    public GameObject menuPanel;
    public GameObject menuCamera;
    public Button hostButton;
    public Button joinButton;
    public TMP_InputField codeInput;
    public TMP_Text statusText;

    [Header("HUD")]
    public GameObject hudPanel;
    public TMP_Text roomCodeText;
    public TMP_Text playersText;

    ISession session;
    string currentCode = "";
    bool busy;
    bool ready;
    bool leaving;
    float countTimer;

    // ---------- helpers ----------
    static async Task WithTimeout(Task task, int seconds, string what)
    {
        var done = await Task.WhenAny(task, Task.Delay(seconds * 1000));
        if (done != task)
            throw new System.TimeoutException(what + " timed out after " + seconds + "s (network may be blocking it)");
        await task;
    }

    static async Task<T> WithTimeout<T>(Task<T> task, int seconds, string what)
    {
        var done = await Task.WhenAny(task, Task.Delay(seconds * 1000));
        if (done != task)
            throw new System.TimeoutException(what + " timed out after " + seconds + "s (network may be blocking it)");
        return await task;
    }

    // ---------- startup ----------
    async void Start()
    {
        ShowMenu("Connecting to Unity services...");
        await SignIn();

        var nm = NetworkManager.Singleton;
        if (nm != null) nm.OnClientDisconnectCallback += OnClientDisconnected;
    }

    void OnDestroy()
    {
        var nm = NetworkManager.Singleton;
        if (nm != null) nm.OnClientDisconnectCallback -= OnClientDisconnected;
    }

    async Task SignIn()
    {
        for (int attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                statusText.text = "Connecting to Unity services...";

                if (UnityServices.State != ServicesInitializationState.Initialized)
                    await WithTimeout(UnityServices.InitializeAsync(), 15, "Unity services init");

                if (!AuthenticationService.Instance.IsSignedIn)
                    await WithTimeout(AuthenticationService.Instance.SignInAnonymouslyAsync(), 15, "Sign in");

                ready = true;
                statusText.text = "Ready";
                return;
            }
            catch (System.Exception e)
            {
                ready = false;
                Debug.LogError("SIGN IN ERROR (attempt " + (attempt + 1) + "): " + e);
                statusText.text = "Could not connect: " + e.Message;

                // a stale saved login can break things the next day, so clear it and retry
                try
                {
                    if (UnityServices.State == ServicesInitializationState.Initialized)
                        AuthenticationService.Instance.ClearSessionToken();
                }
                catch { }
            }
        }
    }

    // ---------- host ----------
    public async void HostRoom()
    {
        if (busy) return;

        busy = true;
        SetMenuInteractable(false);

        try
        {
            if (!ready) await SignIn();
            if (!ready) return;

            statusText.text = "Creating room...";
            Debug.Log("[HOST] creating session with WSS relay");

            var options = new SessionOptions
            {
                MaxPlayers = PartySize
            }.WithRelayNetwork(new RelayNetworkOptions(RelayProtocol.WSS, null, true));

            session = await WithTimeout(
                MultiplayerService.Instance.CreateSessionAsync(options), 20, "Create room");

            currentCode = session.Code;
            Debug.Log("[HOST] room created, code: " + currentCode);

            statusText.text = "Connecting to game...";
            bool connected = await WaitForNetwork();

            if (!connected)
            {
                Debug.LogError("[HOST] network did not start or player did not spawn");
                await LeaveSessionQuietly();
                ShowMenu("Room was created but the game could not start. Check Console.");
                return;
            }

            var nm = NetworkManager.Singleton;
            if (nm == null || nm.LocalClient == null || nm.LocalClient.PlayerObject == null)
            {
                Debug.LogError("[HOST] player object was not spawned");
                await LeaveSessionQuietly();
                ShowMenu("Player failed to spawn.");
                return;
            }

            ShowGame();
            Debug.Log("[HOST] game started");
        }
        catch (System.Exception e)
        {
            Debug.LogError("HOST ERROR: " + e);
            await LeaveSessionQuietly();
            ShowMenu("Could not create room: " + e.Message);
        }
        finally
        {
            busy = false;
            SetMenuInteractable(true);
        }
    }

    // ---------- join ----------
    public async void JoinRoom()
    {
        if (busy) return;

        string code = codeInput.text.Trim().ToUpper();
        if (code.Length == 0)
        {
            statusText.text = "Type a room code first.";
            return;
        }

        busy = true;
        SetMenuInteractable(false);

        try
        {
            if (!ready) await SignIn();
            if (!ready) return;

            statusText.text = "Joining...";
            currentCode = code;
            Debug.Log("[JOIN] joining " + code);

            session = await WithTimeout(
                MultiplayerService.Instance.JoinSessionByCodeAsync(code), 20, "Join room");

            if (!await WaitForNetwork())
            {
                await LeaveSessionQuietly();
                ShowMenu("Joined the room but could not connect.");
                return;
            }

            ShowGame();
        }
        catch (System.Exception e)
        {
            session = null;
            statusText.text = "Could not join: " + e.Message;
            Debug.LogError("JOIN ERROR: " + e);
        }
        finally
        {
            busy = false;
            SetMenuInteractable(true);
        }
    }

    // ---------- leave ----------
    public async void LeaveRoom()
    {
        if (leaving || busy) return;

        leaving = true;
        busy = true;

        try
        {
            await LeaveSessionQuietly();
            currentCode = "";
            ShowMenu("Left the room.");
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error leaving room: " + e);
            ShowMenu("Returned to menu.");
        }
        finally
        {
            leaving = false;
            busy = false;
            SetMenuInteractable(true);
        }
    }

    public void CopyCode()
    {
        GUIUtility.systemCopyBuffer = currentCode;
    }

    // ---------- network ----------
    async Task<bool> WaitForNetwork()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null)
        {
            Debug.LogError("NetworkManager not found!");
            return false;
        }

        for (int i = 0; i < 150; i++)
        {
            if (nm.IsListening &&
                (nm.IsHost || nm.IsConnectedClient) &&
                nm.LocalClient != null &&
                nm.LocalClient.PlayerObject != null)
            {
                Debug.Log("Network connected and player spawned!");
                return true;
            }
            await Task.Delay(100);
        }

        Debug.LogError("Network or player spawning timed out!");
        return false;
    }

    async Task LeaveSessionQuietly()
    {
        try
        {
            if (session != null) await session.LeaveAsync();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning(e);
        }
        session = null;

        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening) nm.Shutdown();
    }

    async void OnClientDisconnected(ulong clientId)
    {
        var nm = NetworkManager.Singleton;
        if (leaving || nm == null || clientId != nm.LocalClientId || !hudPanel.activeSelf) return;

        await Task.Yield();
        leaving = true;
        await LeaveSessionQuietly();
        ShowMenu("Disconnected from the room.");
        leaving = false;
    }

    // ---------- UI ----------
    void Update()
    {
        if (!hudPanel.activeSelf) return;

        var nm = NetworkManager.Singleton;
        if (menuCamera.activeSelf && nm != null && nm.LocalClient != null && nm.LocalClient.PlayerObject != null)
            menuCamera.SetActive(false);

        if (playersText == null) return;
        countTimer -= Time.deltaTime;
        if (countTimer > 0f) return;
        countTimer = 0.5f;

        int count = 0;
        foreach (var no in FindObjectsByType<NetworkObject>(FindObjectsSortMode.None))
            if (no.IsPlayerObject) count++;
        playersText.text = "Players: " + count + "/" + PartySize;
    }

    void ShowGame()
    {
        menuPanel.SetActive(false);
        hudPanel.SetActive(true);
        roomCodeText.text = "Room code: " + currentCode;
    }

    void ShowMenu(string message)
    {
        hudPanel.SetActive(false);
        menuPanel.SetActive(true);
        menuCamera.SetActive(true);
        statusText.text = message;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void SetMenuInteractable(bool on)
    {
        if (hostButton != null) hostButton.interactable = on;
        if (joinButton != null) joinButton.interactable = on;
        if (codeInput != null) codeInput.interactable = on;
    }
}