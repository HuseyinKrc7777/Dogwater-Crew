using Unity.Netcode;
using UnityEditor.Callbacks;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.Animations;

public class TestHoldItem : NetworkBehaviour , IHoldableItem
{
    NetworkVariable<bool>ReadyToHold = new(false);
    Transform holder;
    bool beingHeld = false;
    Rigidbody Rigidbody;
    ParentConstraint constraint;
    Collider Collider;
    string NameOfObjectOnThePlayerPrefab = "HoldingSpherePlayer";
    

    public void Drop()
    {
        Debug.LogError("dropping");
        Vector3 pos = transform.position;
        if(holder!=null)
        {
            pos = holder.position + new Vector3(holder.forward.x,1,holder.forward.z);
        }
        DropServerRpc(pos);
        DropRpc();

    }
    [Rpc(SendTo.Server)]
    private void DropServerRpc(Vector3 pos)
    {
        Debug.LogError("dropping on server");
        
        gameObject.SetActive(true);
        Rigidbody.isKinematic = false;
        Collider.enabled = true;
        Rigidbody.linearVelocity = Vector3.zero;
        transform.position = pos;

    }
    [Rpc(SendTo.Everyone)]
    private void DropRpc(RpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject.transform.Find(NameOfObjectOnThePlayerPrefab).gameObject.SetActive(false);
        Debug.LogError("dropping on everyone");
        
        gameObject.SetActive(true);
        Collider.enabled = true;
    }
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        Rigidbody = GetComponent<Rigidbody>();
        constraint = GetComponent<ParentConstraint>();
        Collider = GetComponent<Collider>();
        Rigidbody.isKinematic = true;

        if(IsServer)
            Drop();
       
        
    }

    public void PickUp(Transform holder)
    {
        if(!ReadyToHold.Value)
            return;
        this.holder = holder;
        
        PickUpVisualRpc();
        PickUpServerRpc();
    }
    [Rpc(SendTo.Server)]
    public void PickUpServerRpc()
    {
        ReadyToHold.Value = false;
        constraint.RemoveSource(0);
        constraint.constraintActive = false;

    }
    
    [Rpc(SendTo.Everyone)]
    public void PickUpVisualRpc(RpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject.transform.Find(NameOfObjectOnThePlayerPrefab).gameObject.SetActive(true);

        gameObject.SetActive(false);
        //oyuncunun prefabında eşyayı tutma durumu olacak , bunu buradam aktifleştireceğiz , eşyayın collisionunu ve modelini kapayacağız.
    }
    

    private void StickToSurface(Transform surfaceTransform)
    {
        //sadece sunucuda çalışmalı
        Rigidbody.isKinematic = true;
        ConstraintSource c = new()
        {
            weight = 1,
            sourceTransform = surfaceTransform
        }; 
        constraint.AddSource(c);
        constraint.constraintActive = true;
        var positionDelta =  transform.position - surfaceTransform.position;
        constraint.SetTranslationOffset(0, Quaternion.Inverse(transform.rotation) * positionDelta);

        ReadyToHold.Value = true;
    }

    private void OnCollisionEnter(Collision other) {
        //sadece sunucuda çalışmalı
        if(!IsServer)
            return;
        if(other.collider.tag == "Ship" || other.collider.tag == "Ground")
        {
           StickToSurface(other.transform);
        }
    }
    public void OnInteract(Player player)
    {
        PickUp(player.transform);
    }

    public void OnUnInteract(Player player)
    {
        Drop();
    }
}
