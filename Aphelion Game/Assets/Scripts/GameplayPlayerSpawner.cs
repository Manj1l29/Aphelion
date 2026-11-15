
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameplayPlayerSpawner : MonoBehaviour
{
    [Header("Gameplay Player")]
    [SerializeField] private NetworkObject gameplayPlayerPrefab;

    [Header("Spawn Point Tag")]
    [SerializeField] private string spawnPointTag = "PlayerSpawn";

    private bool hasSpawnedPlayers;

    private void Awake()
    {
        // Keep this object alive when changing scenes.
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        RegisterSceneCallback();
    }

    private void OnDisable()
    {
        UnregisterSceneCallback();
    }

    private void RegisterSceneCallback()
    {
        NetworkManager nm = NetworkManager.Singleton;

        if (nm != null && nm.SceneManager != null)
        {
            nm.SceneManager.OnLoadEventCompleted -= OnSceneLoaded;
            nm.SceneManager.OnLoadEventCompleted += OnSceneLoaded;

            Debug.Log("[SPAWNER] Scene callback registered.");
        }
    }

    private void UnregisterSceneCallback()
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

        if (nm == null || !nm.IsServer)
            return;

        if (hasSpawnedPlayers)
            return;

        Debug.Log("[SPAWNER] MainScene loaded. Preparing players.");

        if (gameplayPlayerPrefab == null)
        {
            Debug.LogError("[SPAWNER] Gameplay Player Prefab is missing!");
            return;
        }

        // Find every spawn point in MainScene.
        GameObject[] foundPoints;

        try
        {
            foundPoints = GameObject.FindGameObjectsWithTag(spawnPointTag);
        }
        catch (UnityException)
        {
            Debug.LogError("[SPAWNER] Tag '" + spawnPointTag + "' does not exist!");
            return;
        }

        if (foundPoints.Length == 0)
        {
            Debug.LogError("[SPAWNER] No spawn points found!");
            return;
        }

        // Sort points by name so PlayerSpawn1, PlayerSpawn2, etc. stay consistent.
        System.Array.Sort(foundPoints, (a, b) =>
            string.Compare(a.name, b.name, System.StringComparison.Ordinal));

        if (foundPoints.Length < nm.ConnectedClientsList.Count)
        {
            Debug.LogError("[SPAWNER] Not enough spawn points for all players!");
            return;
        }

        // Remove lobby player objects.
        foreach (var client in nm.ConnectedClientsList)
        {
            if (client.PlayerObject != null)
            {
                client.PlayerObject.Despawn(true);
            }
        }

        // Spawn the gameplay player for each connected client.
        int index = 0;

        foreach (var client in nm.ConnectedClientsList)
        {
            Transform spawn = foundPoints[index].transform;

            NetworkObject player = Instantiate(
                gameplayPlayerPrefab,
                spawn.position,
                spawn.rotation
            );

            player.SpawnAsPlayerObject(client.ClientId, true);

            Debug.Log(
                "[SPAWNER] Spawned client " + client.ClientId +
                " at " + spawn.name
            );

            index++;
        }

        hasSpawnedPlayers = true;
        Debug.Log("[SPAWNER] All gameplay players spawned!");
    }
}
