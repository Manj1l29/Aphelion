
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameplayPlayerSpawner : MonoBehaviour
{
    [Header("Gameplay Player")]
    [SerializeField] private NetworkObject gameplayPlayerPrefab;

    private bool hasSpawnedPlayers;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        StartCoroutine(WaitForNetwork());
    }

    private IEnumerator WaitForNetwork()
    {
        while (NetworkManager.Singleton == null ||
               !NetworkManager.Singleton.IsListening)
        {
            yield return null;
        }

        NetworkManager nm = NetworkManager.Singleton;

        if (nm.SceneManager == null)
        {
            Debug.LogError("[SPAWNER] Scene Manager unavailable!");
            yield break;
        }

        nm.SceneManager.OnLoadEventCompleted -= OnSceneLoaded;
        nm.SceneManager.OnLoadEventCompleted += OnSceneLoaded;

        Debug.Log("[SPAWNER] Listening for scene changes.");
    }

    private void OnDisable()
    {
        StopAllCoroutines();

        NetworkManager nm = NetworkManager.Singleton;

        if (nm != null && nm.SceneManager != null)
            nm.SceneManager.OnLoadEventCompleted -= OnSceneLoaded;
    }

    private void OnSceneLoaded(
        string sceneName,
        LoadSceneMode mode,
        List<ulong> clientsCompleted,
        List<ulong> clientsTimedOut)
    {
        if (sceneName != "MainScene")
            return;

        NetworkManager nm = NetworkManager.Singleton;

        if (nm == null || !nm.IsServer || hasSpawnedPlayers)
            return;

        Debug.Log("[SPAWNER] MainScene loaded!");

        if (gameplayPlayerPrefab == null)
        {
            Debug.LogError("[SPAWNER] Gameplay Player Prefab missing!");
            return;
        }

        // Find the spawn point parent in MainScene.
        GameObject spawnParent = GameObject.Find("GameplaySpawnPoints");

        if (spawnParent == null)
        {
            Debug.LogError("[SPAWNER] GameplaySpawnPoints object not found!");
            return;
        }

        // Get the four spawn point transforms by name.
        string[] spawnNames =
        {
            "PlayerSpawn1",
            "PlayerSpawn2",
            "PlayerSpawn3",
            "PlayerSpawn4"
        };

        List<Transform> points = new List<Transform>();

        foreach (string spawnName in spawnNames)
        {
            Transform point = spawnParent.transform.Find(spawnName);

            if (point == null)
            {
                Debug.LogError("[SPAWNER] Missing " + spawnName);
                return;
            }

            points.Add(point);
        }

        int playerCount = nm.ConnectedClientsList.Count;

        if (playerCount > points.Count)
        {
            Debug.LogError("[SPAWNER] Not enough spawn points!");
            return;
        }

        // Remove lobby avatars.
        foreach (var client in nm.ConnectedClientsList)
        {
            if (client.PlayerObject != null)
                client.PlayerObject.Despawn(true);
        }

        // Spawn gameplay players.
        int index = 0;

        foreach (var client in nm.ConnectedClientsList)
        {
            Transform spawn = points[index];

            NetworkObject player = Instantiate(
                gameplayPlayerPrefab,
                spawn.position,
                spawn.rotation
            );

            player.SpawnAsPlayerObject(client.ClientId, true);

            Debug.Log(
                "[SPAWNER] Client " + client.ClientId +
                " spawned at " + spawn.name
            );

            index++;
        }

        hasSpawnedPlayers = true;

        Debug.Log("[SPAWNER] ALL PLAYERS SPAWNED!");
    }
}
