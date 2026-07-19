using DogWater;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

// Moves a player somewhere else on the server's order. Sits on the player prefab.
//
// The move itself has to run on the OWNER: this project's player is owner-authoritative (the owner
// drives a CharacterController and its NetworkTransform replicates the result), so a position written
// on the server would simply be overwritten by the owner on the next frame.
[RequireComponent(typeof(NetworkObject))]
public class PlayerTeleporter : NetworkBehaviour
{
    [Tooltip("All three live on the player prefab. Assign them in the inspector - the same explicit " +
             "wiring ClientPlayerMove uses.")]
    [SerializeField] private CharacterController characterController;
    [SerializeField] private FirstPersonController firstPersonController;
    [SerializeField] private NetworkTransform networkTransform;

    // requestId 0 means the server is not waiting for anything (an arrival). A non-zero id belongs to a
    // departure: the server holds the island scene open until the owner confirms it is no longer
    // standing on it. See IslandManager.SendPlayerToShip.
    [Rpc(SendTo.Owner)]
    public void TeleportRpc(Vector3 position, float yaw, int requestId)
    {
        Apply(position, yaw);

        if (requestId != 0) ConfirmTeleportRpc(requestId);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void ConfirmTeleportRpc(int requestId, RpcParams rpcParams = default)
    {
        if (IslandManager.Instance == null) return;

        // The server reads the sender from the message itself, so a client cannot confirm on someone
        // else's behalf.
        IslandManager.Instance.HandleTeleportConfirmed(rpcParams.Receive.SenderClientId, requestId);
    }

    private void Apply(Vector3 position, float yaw)
    {
        Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);

        if (firstPersonController != null)
        {
            // The ship reference is otherwise only cleared by the ship trigger's OnTriggerExit, and a
            // teleport is not a reliable way to produce one. Left set, the player would keep swaying
            // with the ship's waves while standing on an island.
            firstPersonController.ship = null;

            // Whatever fall the player had built up does not belong to the place they arrive at.
            firstPersonController._verticalVelocity = 0f;
        }

        // A CharacterController writes the transform itself and will fight a direct assignment, so it
        // has to be off while the position is set.
        bool controllerWasEnabled = characterController != null && characterController.enabled;
        if (controllerWasEnabled) characterController.enabled = false;

        transform.SetPositionAndRotation(position, rotation);

        // Without this, observers interpolate across the whole distance and watch the player glide
        // hundreds of metres into the sky. Teleport() may only be called on the authority, which the
        // owner is - unless the prefab is ever switched to a server-authoritative NetworkTransform, in
        // which case it would throw, so the mode is checked rather than assumed.
        if (networkTransform != null && !networkTransform.IsServerAuthoritative())
        {
            networkTransform.Teleport(position, rotation, transform.localScale);
        }

        if (controllerWasEnabled) characterController.enabled = true;
    }
}
