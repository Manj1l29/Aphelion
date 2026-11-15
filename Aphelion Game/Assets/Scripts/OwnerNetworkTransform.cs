
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

public class OwnerNetworkTransform : NetworkTransform
{
    [Header("Initial Spawn Position")]
    public NetworkVariable<Vector3> InitialSpawnPosition =
        new NetworkVariable<Vector3>(
            Vector3.zero,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    public NetworkVariable<Vector3> InitialSpawnEuler =
        new NetworkVariable<Vector3>(
            Vector3.zero,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    public override void OnNetworkSpawn()
    {
        // The owning client must set its position BEFORE
        // NetworkTransform initializes its owner-authoritative state.
        if (IsOwner)
        {
            Vector3 position = InitialSpawnPosition.Value;
            Quaternion rotation =
                Quaternion.Euler(InitialSpawnEuler.Value);

            transform.SetPositionAndRotation(position, rotation);

            Rigidbody rb = GetComponent<Rigidbody>();

            if (rb != null)
            {
                rb.position = position;
                rb.rotation = rotation;
            }

            Debug.Log(
                "[PLAYER TRANSFORM] Owner initialized at: " + position
            );
        }

        // Initialize NetworkTransform only after setting the position.
        base.OnNetworkSpawn();
    }
}
