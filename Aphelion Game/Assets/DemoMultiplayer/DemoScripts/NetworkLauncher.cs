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
        try
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
                await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            ready = true;
            statusText.text = "Ready";
        }
        catch (System.Exception e)
        {
            ready = false;
            statusText.text = "Could not connect. Check internet and Project Settings > Services.";
            Debug.LogError(e);
        }
    }

    public async void HostRoom()
{
    if (busy) return;

    busy = true;
    SetMenuInteractable(false);

    try
    {
        if (!ready)
            await SignIn();

        if (!ready)
            return;

        statusText.text = "Creating room...";

        var options = new SessionOptions
        {
            MaxPlayers = PartySize
        }.WithRelayNetwork();

        session = await MultiplayerService.Instance.CreateSessionAsync(options);

        currentCode = session.Code;

        Debug.Log("Room created! Code: " + currentCode);

        statusText.text = "Connecting to game...";

        bool connected = await WaitForNetwork();

        if (!connected)
        {
            Debug.LogError("Failed to connect or spawn player.");

            await LeaveSessionQuietly();

            ShowMenu("Could not start multiplayer. Try again.");
            return;
        }

        var nm = NetworkManager.Singleton;

        if (nm == null || nm.LocalClient.PlayerObject == null)
        {
            Debug.LogError("Player object was not spawned!");

            await LeaveSessionQuietly();

            ShowMenu("Player failed to spawn.");
            return;
        }

        Debug.Log("Host player spawned successfully!");

        ShowGame();

        Debug.Log("Multiplayer game started!");
    }
    catch (System.Exception e)
    {
        Debug.LogError("HOST ERROR: " + e);

        await LeaveSessionQuietly();

        ShowMenu("Could not create room. Check Console.");
    }
    finally
    {
        busy = false;
        SetMenuInteractable(true);
    }
}


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
            session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code);

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
            statusText.text = "Could not join. Wrong code or room is full.";
            Debug.LogError(e);
        }
        finally
        {
            busy = false;
            SetMenuInteractable(true);
        }
    }

    public async void LeaveRoom()
    {
        leaving = true;
        await LeaveSessionQuietly();
        ShowMenu("Left the room.");
        leaving = false;
    }

    public void CopyCode()
    {
        GUIUtility.systemCopyBuffer = currentCode;
    }

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