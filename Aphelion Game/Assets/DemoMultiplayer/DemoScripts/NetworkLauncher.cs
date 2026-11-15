using System.Collections;
using System.Threading.Tasks;
using TMPro;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections.Generic;
using Unity.Collections;

public class NetworkLauncher : MonoBehaviour
{
    public const int PartySize = 4;
    const string ReadyKey = "ready";
    const string StartGameMessage = "LobbyStartGame";

    public static bool GameStarted { get; private set; }

    enum MenuState { Main, Join, Lobby, Loading }
    MenuState state = MenuState.Main;

    [Header("Scene")]
    public string gameSceneName = "MainScene";
    public float countdownSeconds = 3f;
    public Image fadePanel;          // black Panel (Image), last child of the Canvas
    public float fadeDuration = 1f;

    [Header("Panels")]
    public GameObject mainPanel;
    public GameObject joinPanel;
    public GameObject lobbyPanel;
    public GameObject menuCamera;

    [Header("Main / Join")]
    public Button hostButton;
    public Button joinButton;
    public Button joinConfirmButton;
    public Button backButton;
    public TMP_InputField codeInput;
    public TMP_Text statusText;

    [Header("Lobby")]
    public TMP_Text lobbyCodeText;
    public TMP_Text[] slotNames = new TMP_Text[PartySize];
    public Image[] slotDots = new Image[PartySize];
    public TMP_Text lobbyStatusText;
    public Button readyButton;
    public TMP_Text readyButtonText;
    public int minPlayersToStart = 1;

    [Header("Lobby pads")]
    public Transform[] lobbyPads;     // the 4 podiums, Player 1 to 4
    public float standOffset = 0.02f;

    ISession session;
    string currentCode = "";
    bool busy;
    bool ready;
    bool leaving;
    bool localReady;
    bool counting;
    float countdownLeft;
    float pollTimer;
    Task signInTask;

    static readonly Color EmptyColor = new Color(0.4f, 0.4f, 0.45f);
    static readonly Color JoinedColor = new Color(1f, 0.9f, 0.1f);
    static readonly Color ReadyColor = new Color(0.2f, 1f, 0.3f);

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
    if (fadePanel != null)
    {
        Color c = fadePanel.color;
        c.a = 0f;
        fadePanel.color = c;
        fadePanel.raycastTarget = false;
    }

    GameStarted = false;

    Show(MenuState.Main, "Connecting to Unity services...");

    await SignIn();

    var nm = NetworkManager.Singleton;

    if (nm != null)
    {
        nm.OnClientDisconnectCallback -= OnClientDisconnected;
        nm.OnClientDisconnectCallback += OnClientDisconnected;
    }
}
    

    void OnDestroy()
{
    var nm = NetworkManager.Singleton;

    if (nm != null)
    {
        nm.OnClientDisconnectCallback -= OnClientDisconnected;

        if (nm.CustomMessagingManager != null)
        {
            nm.CustomMessagingManager.UnregisterNamedMessageHandler(
                StartGameMessage
            );
        }
    }
}

    async Task DoSignIn()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
            await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }

    async Task SignIn()
    {
        try
        {
            statusText.text = "Connecting to Unity services...";

            if (signInTask == null || signInTask.IsFaulted || signInTask.IsCanceled)
                signInTask = DoSignIn();

            await WithTimeout(signInTask, 30, "Sign in");

            ready = true;
            statusText.text = "Ready";
        }
        catch (System.Exception e)
        {
            ready = false;
            statusText.text = "Could not connect: " + e.Message;
            Debug.LogError("SIGN IN ERROR: " + e);
        }
    }

    // ---------- screen buttons ----------
    public void ShowJoin()
    {
        if (busy) return;
        Show(MenuState.Join, "");
    }

    public void BackToMain()
    {
        if (busy) return;
        Show(MenuState.Main, "");
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

            var options = new SessionOptions
            {
                MaxPlayers = PartySize
            }.WithRelayNetwork();

            session = await WithTimeout(
                MultiplayerService.Instance.CreateSessionAsync(options), 20, "Create room");

            currentCode = session.Code;
            Debug.Log("[HOST] room created, code: " + currentCode);

            statusText.text = "Connecting to game...";
            bool connected = await WaitForNetwork();

            if (!connected)
            {
                Debug.LogError("[HOST] network did not start or avatar did not spawn");
                await LeaveSessionQuietly();
                Show(MenuState.Main, "Room was created but the game could not start. Check Console.");
                return;
            }

            EnterLobby();
        }
        catch (System.Exception e)
        {
            Debug.LogError("HOST ERROR: " + e);
            await LeaveSessionQuietly();
            Show(MenuState.Main, "Could not create room: " + e.Message);
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

            statusText.text = "Joining session...";

            session = await WithTimeout(
                MultiplayerService.Instance.JoinSessionByCodeAsync(code), 30, "Join room");

            currentCode = string.IsNullOrEmpty(session.Code) ? code : session.Code;
            statusText.text = "Starting network...";

            bool connected = await WaitForNetwork();
            if (!connected)
            {
                Debug.LogError("[JOIN] network connection failed");
                await LeaveSessionQuietly();
                Show(MenuState.Main, "Could not connect to host.");
                return;
            }

            EnterLobby();
        }
        catch (System.Exception e)
        {
            Debug.LogError("[JOIN] FAILED: " + e);
            await LeaveSessionQuietly();
            Show(MenuState.Join, "Could not join: " + e.Message);
        }
        finally
        {
            busy = false;
            SetMenuInteractable(true);
        }
    }

    // ---------- lobby ----------
   void EnterLobby()
{
    Debug.Log("[LOBBY] Entering lobby.");

    localReady = false;
    counting = false;
    GameStarted = false;

    if (readyButtonText != null)
        readyButtonText.text = "Ready";

    RegisterGameStartMessage();

    Show(MenuState.Lobby, "");

    if (lobbyCodeText != null)
        lobbyCodeText.text = currentCode;

    RefreshLobby();
}

    public async void ToggleReady()
    {
        if (session == null || state != MenuState.Lobby) return;
        if (counting && countdownLeft < 1f) return;

        localReady = !localReady;
        if (readyButtonText != null) readyButtonText.text = localReady ? "Unready" : "Ready";

        try
        {
            session.CurrentPlayer.SetProperty(ReadyKey,
                new PlayerProperty(value: localReady ? "1" : "0", visibility: VisibilityPropertyOptions.Member));
            await session.SaveCurrentPlayerDataAsync();
        }
        catch (System.Exception e)
        {
            Debug.LogError("Ready update failed: " + e);
        }
    }

    static bool IsReady(IReadOnlyPlayer p)
    {
        if (p.Properties == null) return false;
        return p.Properties.TryGetValue(ReadyKey, out var prop) && prop.Value == "1";
    }

   void RefreshLobby()
{
    if (session == null) return;

    var players = session.Players;
    int count = players.Count;

    string myId = AuthenticationService.Instance.PlayerId;

    PlaceAvatars();

    bool everyoneReady = count >= minPlayersToStart;

    for (int i = 0; i < PartySize; i++)
    {
        if (slotNames == null || i >= slotNames.Length || slotNames[i] == null)
            continue;

        bool hasDot = slotDots != null &&
                      i < slotDots.Length &&
                      slotDots[i] != null;

        if (i < count)
        {
            bool r = IsReady(players[i]);

            if (!r)
                everyoneReady = false;

            bool me = players[i].Id == myId;

            slotNames[i].text = "Player " + (i + 1) + (me ? " (You)" : "");

            if (hasDot)
                slotDots[i].color = r ? ReadyColor : JoinedColor;
        }
        else
        {
            slotNames[i].text = "Player " + (i + 1);

            if (hasDot)
                slotDots[i].color = EmptyColor;
        }
    }

    if (everyoneReady && !counting)
    {
        counting = true;
        countdownLeft = countdownSeconds;
    }
    else if (!everyoneReady && counting)
    {
        counting = false;
    }

    if (!counting && lobbyStatusText != null)
    {
        lobbyStatusText.text =
            "WAITING FOR PLAYERS... (" + count + "/" + PartySize + ")";
    }
}

    // ---------- podiums (host only) ----------
    
// ---------- podiums ----------
void PlaceAvatars()
{
    var nm = NetworkManager.Singleton;

    if (nm == null || !nm.IsListening || lobbyPads == null)
        return;

    if (nm.LocalClient == null || nm.SpawnManager == null)
        return;

    List<NetworkObject> players = new List<NetworkObject>();

    foreach (var obj in nm.SpawnManager.SpawnedObjectsList)
    {
        if (obj != null && obj.IsPlayerObject)
            players.Add(obj);
    }

    // Host gets podium 1, subsequent players get the next podiums.
    players.Sort((a, b) =>
        a.OwnerClientId.CompareTo(b.OwnerClientId));

    int mySlot = players.FindIndex(
        p => p.OwnerClientId == nm.LocalClientId
    );

    if (mySlot < 0 || mySlot >= lobbyPads.Length)
        return;

    var avatar = nm.LocalClient.PlayerObject;

    if (avatar == null)
        return;

    Transform pad = lobbyPads[mySlot];

    if (pad == null)
        return;

    Vector3 pos = pad.position;

    var rend = pad.GetComponentInChildren<Renderer>();

    if (rend != null)
    {
        pos = new Vector3(
            rend.bounds.center.x,
            rend.bounds.max.y,
            rend.bounds.center.z
        );
    }

    pos.y += standOffset;

    Quaternion rot = pad.rotation;

    if (menuCamera != null)
    {
        Vector3 dir = menuCamera.transform.position - pos;
        dir.y = 0f;

        if (dir.sqrMagnitude > 0.001f)
            rot = Quaternion.LookRotation(dir);
    }

    var controller = avatar.GetComponent<CharacterController>();

    bool wasEnabled = controller != null && controller.enabled;

    if (wasEnabled)
        controller.enabled = false;

    avatar.transform.SetPositionAndRotation(pos, rot);

    if (wasEnabled)
        controller.enabled = true;
}

void OnStartGameMessage(
    ulong senderClientId,
    FastBufferReader reader)
{
    var nm = NetworkManager.Singleton;

    if (nm == null)
        return;

    if (senderClientId != NetworkManager.ServerClientId)
        return;

    if (state == MenuState.Loading)
        return;

    Debug.Log("[LAUNCH] Client received launch message!");

    counting = false;
    GameStarted = true;
    state = MenuState.Loading;

    if (lobbyStatusText != null)
        lobbyStatusText.text = "LAUNCHING...";

    StartCoroutine(FadeScreen());
}

   void FinishCountdown()
{
    if (state == MenuState.Loading)
        return;

    var nm = NetworkManager.Singleton;

    if (nm == null || !nm.IsHost)
        return;

    Debug.Log("[LAUNCH] Countdown reached zero. Host launching.");

    counting = false;
    GameStarted = true;
    state = MenuState.Loading;

    if (lobbyStatusText != null)
        lobbyStatusText.text = "LAUNCHING...";

    Debug.Log("[LAUNCH] Sending launch message to clients.");

    using (var writer = new FastBufferWriter(1, Allocator.Temp))
    {
        nm.CustomMessagingManager.SendNamedMessageToAll(
            StartGameMessage,
            writer,
            NetworkDelivery.Reliable
        );
    }

    StartCoroutine(FadeAndLoad());
}

IEnumerator FadeAndLoad()
{
    Debug.Log("[LAUNCH] Host starting fade.");

    yield return StartCoroutine(FadeScreen());

    // Small delay to let the transition finish rendering.
    yield return new WaitForSecondsRealtime(0.2f);

    var nm = NetworkManager.Singleton;

    if (nm == null)
    {
        Debug.LogError("[SCENE] NetworkManager is NULL!");
        yield break;
    }

    if (!nm.IsHost)
    {
        Debug.LogError("[SCENE] This client is not the host!");
        yield break;
    }

    if (nm.SceneManager == null)
    {
        Debug.LogError("[SCENE] Netcode SceneManager is NULL! Check Enable Scene Management.");
        yield break;
    }

    Debug.Log("[SCENE] Loading: " + gameSceneName);

    var status = nm.SceneManager.LoadScene(
        gameSceneName,
        LoadSceneMode.Single
    );

    Debug.Log("[SCENE] Load result: " + status);

    if (status != SceneEventProgressStatus.Started)
    {
        Debug.LogError("[SCENE] Scene failed to start loading: " + status);
    }
}

IEnumerator FadeScreen()
{
    Debug.Log("[FADE] Starting screen fade.");

    if (fadePanel == null)
    {
        Debug.LogError("[FADE] Fade Panel is NOT assigned!");
        yield break;
    }

    fadePanel.gameObject.SetActive(true);
    fadePanel.raycastTarget = true;

    float t = 0f;

    while (t < fadeDuration)
    {
        t += Time.unscaledDeltaTime;

        Color c = fadePanel.color;
        c.a = Mathf.Clamp01(t / fadeDuration);
        fadePanel.color = c;

        yield return null;
    }

    Color end = fadePanel.color;
    end.a = 1f;
    fadePanel.color = end;

    Debug.Log("[FADE] Screen completely black.");
}

void RegisterGameStartMessage()
{
    var nm = NetworkManager.Singleton;

    if (nm == null || nm.CustomMessagingManager == null)
    {
        Debug.LogError("Network messaging manager is unavailable!");
        return;
    }

    nm.CustomMessagingManager.UnregisterNamedMessageHandler(
        StartGameMessage
    );

    nm.CustomMessagingManager.RegisterNamedMessageHandler(
        StartGameMessage,
        OnStartGameMessage
    );
}

    // ---------- leave ----------
    public async void LeaveRoom()
    {
        if (leaving || busy || state == MenuState.Loading) return;

        leaving = true;
        busy = true;

        try
        {
            await LeaveSessionQuietly();
            currentCode = "";
            Show(MenuState.Main, "Left the room.");
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error leaving room: " + e);
            Show(MenuState.Main, "Returned to menu.");
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
                Debug.Log("Network connected and avatar spawned!");
                return true;
            }
            await Task.Delay(100);
        }

        Debug.LogError("Network or avatar spawning timed out!");
        return false;
    }

    async Task LeaveSessionQuietly()
    {
        GameStarted = false;
        localReady = false;
        counting = false;

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
        if (leaving || nm == null || clientId != nm.LocalClientId || state != MenuState.Lobby) return;

        await Task.Yield();
        leaving = true;
        await LeaveSessionQuietly();
        Show(MenuState.Main, "Disconnected from the room.");
        leaving = false;
    }

    // ---------- UI ----------
    void Update()
{
    if (state != MenuState.Lobby)
        return;

    pollTimer -= Time.deltaTime;

    if (pollTimer <= 0f)
    {
        pollTimer = 0.5f;
        RefreshLobby();
    }

    if (counting)
    {
        countdownLeft -= Time.deltaTime;

        int n = Mathf.Max(1, Mathf.CeilToInt(countdownLeft));

        if (lobbyStatusText != null)
            lobbyStatusText.text = "STARTING IN " + n;

        if (countdownLeft <= 0f)
            FinishCountdown();
    }
}

    void Show(MenuState newState, string message)
{
    state = newState;

    if (mainPanel != null)
        mainPanel.SetActive(newState == MenuState.Main);

    if (joinPanel != null)
        joinPanel.SetActive(newState == MenuState.Join);

    if (lobbyPanel != null)
        lobbyPanel.SetActive(
            newState == MenuState.Lobby ||
            newState == MenuState.Loading
        );

    if (menuCamera != null)
        menuCamera.SetActive(true);

    if (!string.IsNullOrEmpty(message) && statusText != null)
        statusText.text = message;

    Cursor.lockState = CursorLockMode.None;
    Cursor.visible = true;
}
    void SetMenuInteractable(bool on)
    {
        if (hostButton != null) hostButton.interactable = on;
        if (joinButton != null) joinButton.interactable = on;
        if (joinConfirmButton != null) joinConfirmButton.interactable = on;
        if (backButton != null) backButton.interactable = on;
        if (codeInput != null) codeInput.interactable = on;
    }
}