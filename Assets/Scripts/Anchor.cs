using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class Anchor : NetworkBehaviour, IInteractable, IHandInput
{
    public const float FullyDeployedRopeThreshold = 70f;

    public NetworkVariable<float> releasedRopeAmount = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> isRopeFree = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> holdingPlayerCount = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    
    public float maxRope = 100;
    public float ropeDescentMultiplier = 10f;

    [Header("Debug")]
    [SerializeField] private bool logRopeAmount = true;
    [Min(0.1f)][SerializeField] private float ropeLogIntervalSeconds = 0.5f;

    public bool IsFullyDeployed => releasedRopeAmount.Value > FullyDeployedRopeThreshold;

    // The NetworkVariable is the replicated display value. The server-side set is the source of truth:
    // repeated interact requests cannot count the same client twice, and a disconnect can remove the
    // exact holder instead of guessing by decrementing a shared counter.
    private readonly HashSet<ulong> holdingClients = new HashSet<ulong>();
    private bool subscribedToDisconnects;
    private float lastLoggedRopeAmount = float.NaN;
    private float nextRopeLogTime;

    public void OnButtonInput()
    {
        changeRopeRpc(!isRopeFree.Value);
    }
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void changeRopeRpc(bool value)
    {
        isRopeFree.Value = value;
    }

    public void OnHandInput(float xValue, float yValue)
    {
        if(isRopeFree.Value)
        {
            pullAnchorRpc(Mathf.Abs(yValue));
            pullAnchorRpc(Mathf.Abs(xValue));
        }
    }

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

        if (holdingClients.Add(clientId))
        {
            SyncHoldingPlayerCount();
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void DecreaseHoldingPlayerRpc(RpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;

        if (holdingClients.Remove(clientId))
        {
            SyncHoldingPlayerCount();
        }
    }

    public void OnInteract(Player player)
    {
        IncreaseHoldingPlayerRpc();
    }

    public void OnRightHandInput(float xValue, float yValue)
    {
    }

    public void OnUnInteract(Player player)
    {
        DecreaseHoldingPlayerRpc();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        lastLoggedRopeAmount = float.NaN;
        nextRopeLogTime = 0f;
        LogRopeAmountIfNeeded();

        if (!IsServer) return;

        holdingClients.Clear();
        SyncHoldingPlayerCount();

        NetworkManager.OnClientDisconnectCallback += HandleClientDisconnect;
        subscribedToDisconnects = true;
    }

    public override void OnNetworkDespawn()
    {
        UnsubscribeFromDisconnects();

        base.OnNetworkDespawn();
    }

    public override void OnDestroy()
    {
        UnsubscribeFromDisconnects();

        base.OnDestroy();
    }

    private void HandleClientDisconnect(ulong clientId)
    {
        if (!IsServer || !IsSpawned) return;

        if (holdingClients.Remove(clientId))
        {
            SyncHoldingPlayerCount();
        }
    }

    private void SyncHoldingPlayerCount()
    {
        holdingPlayerCount.Value = holdingClients.Count;
    }

    private void UnsubscribeFromDisconnects()
    {
        if (!subscribedToDisconnects) return;

        if (NetworkManager != null)
        {
            NetworkManager.OnClientDisconnectCallback -= HandleClientDisconnect;
        }

        subscribedToDisconnects = false;
    }

    private void LogRopeAmountIfNeeded()
    {
        if (!logRopeAmount) return;

        float currentAmount = releasedRopeAmount.Value;
        if (!float.IsNaN(lastLoggedRopeAmount) && Mathf.Approximately(currentAmount, lastLoggedRopeAmount)) return;
        if (Time.unscaledTime < nextRopeLogTime) return;

        string deploymentState = IsFullyDeployed ? "Evet" : "Hayır";
        Debug.Log($"Çapa halat miktarı: {currentAmount:F1} | Tamamen atıldı: {deploymentState}", this);

        lastLoggedRopeAmount = currentAmount;
        nextRopeLogTime = Time.unscaledTime + Mathf.Max(0.1f, ropeLogIntervalSeconds);
    }

    void Update()
    {
        LogRopeAmountIfNeeded();

        if (!IsServer) return;

        if (holdingPlayerCount.Value == 0 && isRopeFree.Value && releasedRopeAmount.Value < maxRope)
        {
            releasedRopeAmount.Value = Mathf.Min(
                maxRope,
                releasedRopeAmount.Value + Time.deltaTime * ropeDescentMultiplier);
        }
    }
}
