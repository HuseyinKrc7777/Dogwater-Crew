using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class AnchorNetworkController : NetworkBehaviour
{
    public Anchor anchor;
    public NetworkVariable<float> releasedRopeAmount = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> isRopeFree = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> holdingPlayerCount = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    
    

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void pullAnchorRpc(float value)
    {
        if (releasedRopeAmount.Value > 0)
        {
            releasedRopeAmount.Value = Mathf.Max(0f, releasedRopeAmount.Value - value);
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void IncreaseHoldingPlayerRpc(RpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;

        if (anchor.holdingClients.Add(clientId))
        {
            anchor.SyncHoldingPlayerCount();
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void DecreaseHoldingPlayerRpc(RpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;

        if (anchor.holdingClients.Remove(clientId))
        {
            anchor.SyncHoldingPlayerCount();
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void changeRopeRpc(bool value)
    {
        isRopeFree.Value = value;
    }


    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        anchor.controller = this;

        anchor.lastLoggedRopeAmount = float.NaN;
        anchor.nextRopeLogTime = 0f;
        anchor.LogRopeAmountIfNeeded();

        if (!IsServer) return;

        anchor.holdingClients.Clear();
        anchor.SyncHoldingPlayerCount();

        NetworkManager.OnClientDisconnectCallback += anchor.HandleClientDisconnect;
        anchor.subscribedToDisconnects = true;
    }

    public override void OnNetworkDespawn()
    {
        anchor.UnsubscribeFromDisconnects();

        base.OnNetworkDespawn();
    }

    public override void OnDestroy()
    {
        anchor.UnsubscribeFromDisconnects();

        base.OnDestroy();
    }


    
   

    

    

}
