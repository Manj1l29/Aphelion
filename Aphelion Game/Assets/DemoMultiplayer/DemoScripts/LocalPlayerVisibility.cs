
using Unity.Netcode;
using UnityEngine;

public class LocalPlayerVisibility : NetworkBehaviour
{
    [Header("Player Model")]
    public Renderer[] bodyRenderers;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;

        foreach (Renderer rend in bodyRenderers)
        {
            rend.enabled = false;
        }
    }
}
