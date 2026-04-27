using Unity.Netcode;
using UnityEngine;

public class Cannonball : NetworkBehaviour
{
    [Header("Settings")]
    [SerializeField] private float lifeTime = 5f;

    public override void OnNetworkSpawn()
    {
        
        if (IsServer)
        {
            Invoke(nameof(DespawnBall), lifeTime);
        }
    }

    private void DespawnBall()
    {
        if (IsServer && NetworkObject != null && NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn(true);
        }
    }
}
