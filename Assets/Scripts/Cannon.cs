using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class Cannon : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject cannonballPrefab;
    [SerializeField] private Transform spawnPoint;

    [Header("Settings")]
    [SerializeField] private float fireForce = 20f;

    void Update()
    {
        
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            RequestFireRpc();
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestFireRpc(RpcParams rpcParams = default)
    {
        
        GameObject cannonballInstance = Instantiate(cannonballPrefab, spawnPoint.position, spawnPoint.rotation);

        
        if (cannonballInstance.TryGetComponent<Rigidbody>(out Rigidbody rb))
        {
            rb.linearVelocity = spawnPoint.forward * fireForce;
        }

        
        if (cannonballInstance.TryGetComponent<NetworkObject>(out NetworkObject netObj))
        {
            netObj.Spawn();
        }
    }
}
