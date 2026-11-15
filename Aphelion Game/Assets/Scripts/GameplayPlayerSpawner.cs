
using System.Collections.Generic;
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

        if (nm == null || !nm.IsServer)
            return;

        if (hasSpawnedPlayers)
            return;

        hasSpawnedPlayers = true;

        if (gameplayPlayerPrefab == null)
        {
            Debug.LogError("[SPAWNER] Player prefab is not assigned!");
            return;
        }

        Debug.Log("[SPAWNER] MainScene loaded. Spawning gameplay players.");

        // Remove the old lobby player objects.
        foreach (var client in nm.ConnectedClientsList)
        {
            if (client.PlayerObject != null)
            {
                client.PlayerObject.Despawn(true);
            }
        }

        // Spawn a playable player for each connected client.
        int index = 0;

        foreach (var client in nm.ConnectedClientsList)
        {
            if (index >= spawnPoints.Length || spawnPoints[index] == null)
            {
                Debug.LogError("[SPAWNER] Missing spawn point " + index);
                index++;
                continue;
            }

            Transform spawn = spawnPoints[index];

            NetworkObject player = Instantiate(
                gameplayPlayerPrefab,
                spawn.position,
                spawn.rotation
            );

            player.SpawnAsPlayerObject(client.ClientId, true);

            Debug.Log(
                "[SPAWNER] Spawned player for client " +
                client.ClientId
            );

            index++;
        }
    }
}
