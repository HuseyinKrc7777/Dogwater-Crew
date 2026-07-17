using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
interface IDamageDealer
{
    float DamageAmount {get ; set;}
}
public class Cannonball : NetworkBehaviour , IDamageDealer
{
    [Header("Settings")]
    [SerializeField] private float lifeTime = 5f;
    [SerializeField] private float damageAmount;
    public float DamageAmount { get{DespawnBall(); return damageAmount;} set => DamageAmount = value; }

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
