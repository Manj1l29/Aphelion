
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameplayPlayerSpawner : MonoBehaviour
{
    [Header("Gameplay Player")]
    public NetworkObject gameplayPlayerPrefab;

    [Header("Spawn Points")]
    public Transform[] spawnPoints = new Transform[4];

    private bool hasSpawnedPlayers;

    private void OnEnable()
    {
        var nm = NetworkManager.Singleton;

        if (nm != null && nm.SceneManager != null)
        {
            nm.SceneManager.OnLoadEventCompleted -= OnSceneLoaded;
            nm.SceneManager.OnLoadEventCompleted += OnSceneLoaded;
        }
    }

    private void OnDisable()
    {
        var nm = NetworkManager.Singleton;

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

        var nm = NetworkManager.Singleton;

        if (nm == null || !nm.IsServer || hasSpawnedPlayers)
            return;

        Debug.Log("[SPAWNER] MainScene loaded. Preparing players.");

        if (gameplayPlayerPrefab == null)
        {
            Debug.LogError("[SPAWNER] Player prefab is not assigned!");
            return;
        }

        // Sort clients so spawn assignments are consistent.
        var clients = nm.ConnectedClientsList
            .OrderBy(client => client.ClientId)
            .ToList();

        // Validate all required spawn points first.
        if (spawnPoints == null || spawnPoints.Length < clients.Count)
        {
            Debug.LogError("[SPAWNER] Not enough spawn point slots!");
            return;
        }

        for (int i = 0; i < clients.Count; i++)
        {
            if (spawnPoints[i] == null)
            {
                Debug.LogError(
                    "[SPAWNER] Spawn Point " + (i + 1) +
                    " is EMPTY in the Inspector!"
                );
                return;
            }
        }

        // Remove old lobby player objects.
        foreach (var client in clients)
        {
            if (client.PlayerObject != null)
            {
                client.PlayerObject.Despawn(true);
            }
        }

        // Spawn gameplay players at their assigned positions.
        for (int i = 0; i < clients.Count; i++)
        {
            var client = clients[i];
            Transform spawn = spawnPoints[i];

            NetworkObject player = Instantiate(
                gameplayPlayerPrefab,
                spawn.position,
                spawn.rotation
            );

            player.SpawnAsPlayerObject(client.ClientId, true);

            Debug.Log(
                "[SPAWNER] Client " + client.ClientId +
                " spawned at " + spawn.name +
                " | Position: " + spawn.position
            );
        }

        hasSpawnedPlayers = true;

        Debug.Log("[SPAWNER] ALL GAMEPLAY PLAYERS SPAWNED!");
    }
}
