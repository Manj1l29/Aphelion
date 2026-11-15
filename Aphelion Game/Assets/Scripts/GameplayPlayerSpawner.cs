
using System.Collections;
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
        // This object MUST be a root GameObject.
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        StartCoroutine(WaitForNetwork());
    }

    private IEnumerator WaitForNetwork()
    {
        // Wait until Netcode has started.
        while (NetworkManager.Singleton == null ||
               !NetworkManager.Singleton.IsListening)
        {
            yield return null;
        }

        NetworkManager nm = NetworkManager.Singleton;

        if (nm.SceneManager == null)
        {
            Debug.LogError("[SPAWNER] Network Scene Management is unavailable!");
            yield break;
        }

        nm.SceneManager.OnLoadEventCompleted -= OnSceneLoaded;
        nm.SceneManager.OnLoadEventCompleted += OnSceneLoaded;

        Debug.Log("[SPAWNER] Listening for scene load completion.");
    }

    private void OnDisable()
    {
        StopAllCoroutines();

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

        // Only the host/server spawns players.
        if (nm == null || !nm.IsServer)
            return;

        if (hasSpawnedPlayers)
            return;

        Debug.Log("[SPAWNER] MainScene finished loading for clients.");

        if (gameplayPlayerPrefab == null)
        {
            Debug.LogError("[SPAWNER] Gameplay Player Prefab is missing!");
            return;
        }

        // Find spawn points in MainScene.
        GameObject[] foundPoints;

        try
        {
            foundPoints = GameObject.FindGameObjectsWithTag(spawnPointTag);
        }
        catch (UnityException)
        {
            Debug.LogError("[SPAWNER] PlayerSpawn tag does not exist!");
            return;
        }

        System.Array.Sort(foundPoints, (a, b) =>
            string.Compare(a.name, b.name, System.StringComparison.Ordinal));

        if (foundPoints.Length < nm.ConnectedClientsList.Count)
        {
            Debug.LogError("[SPAWNER] Not enough spawn points!");
            return;
        }

        // Make sure every point is available before replacing lobby players.
        if (foundPoints.Length == 0)
        {
            Debug.LogError("[SPAWNER] No spawn points found!");
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

        // Spawn the actual gameplay players.
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
                "[SPAWNER] Client " + client.ClientId +
                " spawned at " + foundPoints[index].name
            );

            index++;
        }

        hasSpawnedPlayers = true;

        Debug.Log("[SPAWNER] All gameplay players spawned!");
    }
}
