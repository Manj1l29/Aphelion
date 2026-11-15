
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

        Debug.Log("[SPAWNER] MainScene loaded.");

        if (gameplayPlayerPrefab == null)
        {
            Debug.LogError("[SPAWNER] Gameplay player prefab missing!");
            return;
        }

        // Host first, followed by clients in ClientId order.
        var clients = nm.ConnectedClientsList
            .OrderBy(client => client.ClientId)
            .ToList();

        if (spawnPoints == null || spawnPoints.Length < clients.Count)
        {
            Debug.LogError("[SPAWNER] Not enough spawn points!");
            return;
        }

        // Validate all spawn points.
        for (int i = 0; i < clients.Count; i++)
        {
            if (spawnPoints[i] == null)
            {
                Debug.LogError("[SPAWNER] Spawn Point " +
                               (i + 1) + " is not assigned!");
                return;
            }

            Debug.Log("[SPAWNER] Point " + (i + 1) +
                      " position: " + spawnPoints[i].position);
        }

        // Remove the lobby player objects.
        foreach (var client in clients)
        {
            if (client.PlayerObject != null)
            {
                client.PlayerObject.Despawn(true);
            }
        }

        // Spawn each gameplay player at their assigned point.
        for (int i = 0; i < clients.Count; i++)
        {
            var client = clients[i];
            Transform spawn = spawnPoints[i];

            Vector3 spawnPosition = spawn.position;
            Quaternion spawnRotation = spawn.rotation;

            // Instantiate at the exact spawn position.
            NetworkObject player = Instantiate(
                gameplayPlayerPrefab,
                spawnPosition,
                spawnRotation
            );

            // Handle CharacterController if present.
            CharacterController controller =
                player.GetComponent<CharacterController>();

            bool controllerWasEnabled =
                controller != null && controller.enabled;

            if (controller != null)
                controller.enabled = false;

            // Explicitly set the player transform.
            player.transform.SetPositionAndRotation(
                spawnPosition,
                spawnRotation
            );

            // Synchronize Rigidbody position if present.
            Rigidbody rb = player.GetComponent<Rigidbody>();

            if (rb != null)
            {
                rb.position = spawnPosition;
                rb.rotation = spawnRotation;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            if (controller != null)
                controller.enabled = controllerWasEnabled;

            Physics.SyncTransforms();

            // Network-spawn as the assigned client's player.
            player.SpawnAsPlayerObject(client.ClientId, true);

            Debug.Log(
                "[SPAWNER] P" + (i + 1) +
                " | Client ID: " + client.ClientId +
                " | Spawn Point: " + spawn.name +
                " | Position: " + player.transform.position
            );
        }

        hasSpawnedPlayers = true;

        Debug.Log("[SPAWNER] ALL PLAYERS SPAWNED!");
    }
}
