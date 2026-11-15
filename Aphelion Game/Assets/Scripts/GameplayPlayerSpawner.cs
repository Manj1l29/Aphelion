
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameplayPlayerSpawner : MonoBehaviour
{
    [Header("Gameplay Player")]
    [SerializeField] private NetworkObject gameplayPlayerPrefab;

    [Header("Spawn Points")]
    [SerializeField] private Transform[] spawnPoints = new Transform[4];

    private bool hasSpawnedPlayers;

    private void Awake()
    {
        // This object must be a root GameObject.
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        NetworkManager nm = NetworkManager.Singleton;

        if (nm != null && nm.IsListening && nm.SceneManager != null)
        {
            nm.SceneManager.OnLoadEventCompleted -= OnSceneLoaded;
            nm.SceneManager.OnLoadEventCompleted += OnSceneLoaded;
        }
    }

    private void OnDisable()
    {
        NetworkManager nm = NetworkManager.Singleton;

        if (nm != null && nm.SceneManager != null)
        {
            nm.SceneManager.OnLoadEventCompleted -= OnSceneLoaded;
        }
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

        Debug.Log("[SPAWNER] MainScene loaded. Preparing players.");

        if (gameplayPlayerPrefab == null)
        {
            Debug.LogError("[SPAWNER] Gameplay Player Prefab is missing!");
            return;
        }

        int playerCount = nm.ConnectedClientsList.Count;

        if (spawnPoints == null || spawnPoints.Length < playerCount)
        {
            Debug.LogError("[SPAWNER] Not enough spawn point slots!");
            return;
        }

        // Validate every required spawn point before removing lobby players.
        for (int i = 0; i < playerCount; i++)
        {
            if (spawnPoints[i] == null)
            {
                Debug.LogError("[SPAWNER] Spawn Point " + (i + 1) + " is not assigned!");
                return;
            }
        }

        // Remove the lobby avatars.
        foreach (var client in nm.ConnectedClientsList)
        {
            if (client.PlayerObject != null)
            {
                client.PlayerObject.Despawn(true);
            }
        }

        // Spawn gameplay players at their assigned positions.
        int index = 0;

        foreach (var client in nm.ConnectedClientsList)
        {
            Transform spawn = spawnPoints[index];

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

        Debug.Log("[SPAWNER] All gameplay players spawned successfully!");
    }
}
