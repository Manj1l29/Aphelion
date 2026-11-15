
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

    private void OnEnable()
    {
        NetworkManager nm = NetworkManager.Singleton;

        if (nm != null && nm.SceneManager != null)
        {
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

        Debug.Log("[SPAWNER] MainScene loaded.");

        if (gameplayPlayerPrefab == null)
        {
            Debug.LogError("[SPAWNER] Gameplay prefab is missing!");
            return;
        }

        // Ensure the prefab uses our custom NetworkTransform.
        if (gameplayPlayerPrefab.GetComponent<OwnerNetworkTransform>() == null)
        {
            Debug.LogError(
                "[SPAWNER] OwnerNetworkTransform is missing from Player prefab!"
            );
            return;
        }

        // Get and sort connected clients.
        List<NetworkClient> clients =
            new List<NetworkClient>(nm.ConnectedClientsList);

        clients.Sort((a, b) => a.ClientId.CompareTo(b.ClientId));

        Debug.Log("[SPAWNER] Connected clients: " + clients.Count);

        if (clients.Count == 0)
        {
            Debug.LogError("[SPAWNER] No connected clients!");
            return;
        }

        // Validate spawn points.
        if (spawnPoints == null || spawnPoints.Length < clients.Count)
        {
            Debug.LogError("[SPAWNER] Not enough spawn points!");
            return;
        }

        for (int i = 0; i < clients.Count; i++)
        {
            if (spawnPoints[i] == null)
            {
                Debug.LogError(
                    "[SPAWNER] Spawn point " + i + " is missing!"
                );
                return;
            }

            Debug.Log(
                "[SPAWNER] Client " + clients[i].ClientId +
                " assigned to " + spawnPoints[i].name +
                " at " + spawnPoints[i].position
            );
        }

        hasSpawnedPlayers = true;

        // Remove lobby player objects.
        foreach (NetworkClient client in clients)
        {
            if (client.PlayerObject != null &&
                client.PlayerObject.IsSpawned)
            {
                client.PlayerObject.Despawn(true);
            }
        }

        // Spawn each gameplay player.
        for (int i = 0; i < clients.Count; i++)
        {
            NetworkClient client = clients[i];
            Transform spawn = spawnPoints[i];

            Vector3 position = spawn.position;
            Vector3 eulerRotation = spawn.eulerAngles;

            Debug.Log(
                "[SPAWNER] Preparing client " +
                client.ClientId + " at " + position
            );

            // Instantiate at a neutral position.
            NetworkObject player = Instantiate(
                gameplayPlayerPrefab,
                Vector3.zero,
                Quaternion.identity
            );

            // Send the correct spawn position to the owner.
            OwnerNetworkTransform ownerTransform =
                player.GetComponent<OwnerNetworkTransform>();

            ownerTransform.InitialSpawnPosition.Value = position;
            ownerTransform.InitialSpawnEuler.Value = eulerRotation;

            // Spawn as the connected client's player object.
            player.SpawnAsPlayerObject(client.ClientId, true);

            Debug.Log(
                "[SPAWNER] Spawned player for client " +
                client.ClientId
            );
        }

        Debug.Log("[SPAWNER] Gameplay spawning complete.");
    }
}
