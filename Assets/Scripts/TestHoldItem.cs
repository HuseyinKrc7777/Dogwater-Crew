using Unity.Netcode;
using UnityEditor.Callbacks;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Rendering;

public class TestHoldItem : NetworkBehaviour , IHoldableItem
{
    Transform holder;
    Collider Collider;
    //objenin meshini oyuncu prefabında bulunması lazım
    string NameOfObjectOnThePlayerPrefab = "HoldingSpherePlayer";
    //objenin altında çocuk olarak bulunacak bir mesh ' e ihtiyaç vardır
    GameObject mesh;
    GameObject spawnedMesh;

    public IHoldableItem originalItem { get => this; set => throw new System.NotImplementedException(); }
    [Rpc(SendTo.Server)]
    private void JoinRpc()
    {
        Drop();
    }
    public void Drop()
    {
        Debug.LogError("dropping");
        Vector3 pos = transform.position;
        if(holder!=null)
        {
            pos = holder.position + new Vector3(holder.forward.x,1,holder.forward.z)*2;
        }
        if(spawnedMesh!=null)
        {
            pos = spawnedMesh.transform.position;
            UnStickRpc();
        }
        DropServerRpc(pos);
        DropRpc(pos);
        holder = null;
    }
    [Rpc(SendTo.Server)]
    private void DropServerRpc(Vector3 pos)
    {
        Debug.LogError("dropping on server");
        gameObject.SetActive(true);
        Collider.enabled = true;
        LookForSurface(pos);

    }
    [Rpc(SendTo.Everyone)]
    private void DropRpc(Vector3 pos,RpcParams rpcParams = default)
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
        
        Collider = GetComponent<Collider>();
        mesh = transform.Find("mesh").gameObject;
        mesh.SetActive(false);
        if(IsServer)
            Drop();
        else
            JoinRpc();
       
        
    }


    public void PickUp(Transform holder)
    {
        this.holder = holder;
        UnStickRpc();
        PickUpVisualRpc();
    }

    [Rpc(SendTo.Everyone)]
    public void PickUpVisualRpc(RpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject.transform.Find(NameOfObjectOnThePlayerPrefab).gameObject.SetActive(true);

        gameObject.SetActive(false);
        //oyuncunun prefabında eşyayı tutma durumu olacak , bunu buradam aktifleştireceğiz , eşyayın collisionunu ve modelini kapayacağız.
    }
    

    private void StickToSurface(Transform surfaceTransform,Vector3 pos,Quaternion rot)
    {
        //sadece sunucuda çalışmalı
        ulong surfaceId= 0;
        bool inworld = false;
        bool inWater = false;

        if(surfaceTransform!=null)
        {
            surfaceTransform.TryGetComponent(out NetworkObject nobj);
            if(nobj!=null)
                surfaceId = nobj.NetworkObjectId;
            else
                inworld = true;
        }
        else
        {
            inWater = true;
            inworld = true;            
        }
        StickRpc(inworld,surfaceId,pos,rot,inWater);
    }
    public float objectHeight = 0.5f; 
    [Rpc(SendTo.Everyone)]
    private void StickRpc(bool inWorld,ulong shipId,Vector3 pos,Quaternion rot,bool inWater)
    {
        Transform obj = null;
        if(!inWorld)
            obj = NetworkManager.Singleton.SpawnManager.SpawnedObjects[(ulong)shipId].transform;
        spawnedMesh = Instantiate(mesh);
        spawnedMesh.transform.SetPositionAndRotation(pos,rot);
        spawnedMesh.transform.parent = obj;
        if(inWater)
        {
            spawnedMesh.GetComponent<holdabkeobjectmeshextension>().stuckToFloor = false;
        }
        spawnedMesh.GetComponent<IHoldableItem>().originalItem = this;
        spawnedMesh.SetActive(true);

    }
    [Rpc(SendTo.Everyone)]
    private void UnStickRpc()
    {
        Debug.LogError("destroying the mesh");
        spawnedMesh.SetActive(false);
        Destroy(spawnedMesh,1);
    }
    private void LookForSurface(Vector3 pos)
    {   
        RaycastHit hit;
        int layer = 1 << LayerMask.NameToLayer("Default");
        Physics.Raycast(pos, Vector3.down, out hit, 10f,layer,queryTriggerInteraction:QueryTriggerInteraction.Ignore);
        if (hit.collider != null)
        {
            StickToSurface(hit.transform,hit.point + hit.transform.up * objectHeight,hit.transform.rotation);
        }
        else
        {
            StickToSurface(null,pos,transform.rotation);
            
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
