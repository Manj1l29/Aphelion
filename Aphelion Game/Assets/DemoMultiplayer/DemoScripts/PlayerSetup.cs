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
        if (IsOwner)
{
    int bodyLayer = LayerMask.NameToLayer("LocalBody");

    foreach (var r in GetComponentsInChildren<Renderer>(true))
        r.gameObject.layer = bodyLayer;

    var cam = GetComponentInChildren<Camera>(true);
    cam.cullingMask &= ~(1 << bodyLayer);
}   

        controller.enabled = false;
        transform.position = new Vector3(((int)OwnerClientId % 4) * 2f - 3f, 1f, 0f);
        controller.enabled = true;
        movement.SetCursorLocked(true);
    }
}