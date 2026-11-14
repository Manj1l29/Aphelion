using Unity.Netcode;
using UnityEngine;

public class PlayerSetup : NetworkBehaviour
{
    public Camera playerCamera;
    public AudioListener listener;
    public PlayerMovement movement;
    public CharacterController controller;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            playerCamera.enabled = false;
            listener.enabled = false;
            movement.enabled = false;
            controller.enabled = false;
            return;
        }

        controller.enabled = false;
        transform.position = new Vector3(((int)OwnerClientId % 4) * 2f - 3f, 1f, 0f);
        controller.enabled = true;
        movement.SetCursorLocked(true);
    }
}