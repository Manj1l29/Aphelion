using Unity.Netcode;
using UnityEngine;

public class PlayerSetup : NetworkBehaviour
{
    public Camera playerCamera;
    public AudioListener listener;
    public PlayerMovement movement;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            playerCamera.enabled = false;
            listener.enabled = false;
            movement.enabled = false;
            return;
        }

        // Spread players out so they don't spawn inside each other
        Vector3 spawnPos = new Vector3(((int)OwnerClientId % 4) * 2f - 3f, 1f, 0f);

        Rigidbody rb = GetComponent<Rigidbody>();
        rb.position = spawnPos;
        transform.position = spawnPos;
    }
}