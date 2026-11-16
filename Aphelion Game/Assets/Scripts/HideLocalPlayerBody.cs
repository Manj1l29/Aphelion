
using Unity.Netcode;
using UnityEngine;

public class HideLocalPlayerBody : NetworkBehaviour
{
    [Header("Character Model")]
    [SerializeField] private Transform bodyRoot;

    public override void OnNetworkSpawn()
    {
        // Only hide the body for the player controlling this character.
        if (!IsOwner)
            return;

        if (bodyRoot == null)
        {
            Debug.LogError(
                "[PLAYER BODY] Body Root is not assigned!"
            );
            return;
        }

        // Get all renderers belonging to the character model.
        Renderer[] bodyRenderers =
            bodyRoot.GetComponentsInChildren<Renderer>(true);

        foreach (Renderer bodyRenderer in bodyRenderers)
        {
            bodyRenderer.enabled = false;
        }

        Debug.Log("[PLAYER BODY] Hidden local player model.");
    }
}
