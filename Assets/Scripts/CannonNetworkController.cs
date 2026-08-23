using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class CannonNetworkController : NetworkBehaviour
{
    public List<Cannon> cannons = new();
    public NetworkVariable<float> cannonYaw = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<float> cannonPitch = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        foreach(Cannon cannon in cannons)
        {
            cannon.index = cannons.IndexOf(cannon);
            cannon.controller = this;
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RotateCannonRpc(int index,float yawDelta, float pitchDelta)
    {
        // Açıları sınırlar içinde (eser miktarda) tutuyoruz
        Cannon cannon = cannons[index];
        float newYaw = Mathf.Clamp(cannonYaw.Value + yawDelta, -cannon.maxYaw, cannon.maxYaw);
        float newPitch = Mathf.Clamp(cannonPitch.Value + pitchDelta, cannon.minPitch, cannon.maxPitch);
        
        cannonYaw.Value = newYaw;
        cannonPitch.Value = newPitch;
    }

   

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestFireRpc(int index,RpcParams rpcParams = default)
    {
        Cannon cannon = cannons[index];

        GameObject cannonballInstance = Instantiate(cannon.cannonballPrefab, cannon.spawnPoint.position, cannon.spawnPoint.rotation);

        if (cannonballInstance.TryGetComponent<Rigidbody>(out Rigidbody rb))
        {
            // Namlunun baktığı yöne (spawnPoint.forward) doğru fırlat!
            rb.linearVelocity = cannon.spawnPoint.forward * cannon.fireForce + GetComponentInParent<Ship>().gameObject.GetComponent<Rigidbody>().linearVelocity;
            rb.angularVelocity =  GetComponentInParent<Ship>().gameObject.GetComponent<Rigidbody>().angularVelocity;
        
        }

        if (cannonballInstance.TryGetComponent<NetworkObject>(out NetworkObject netObj))
        {
            netObj.Spawn();
        }
    }

   
}