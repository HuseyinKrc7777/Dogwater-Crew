using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class Anchor : MonoBehaviour, IInteractable, IHandInput
{
    public const float FullyDeployedRopeThreshold = 70f;

    public AnchorNetworkController controller;
       public float maxRope = 100;
    public float ropeDescentMultiplier = 10f;

    [Header("Debug")]
    [SerializeField] public bool logRopeAmount = true;
    [Min(0.1f)][SerializeField] public float ropeLogIntervalSeconds = 0.5f;

    public bool IsFullyDeployed => controller.releasedRopeAmount.Value > FullyDeployedRopeThreshold;

    // The NetworkVariable is the replicated display value. The server-side set is the source of truth:
    // repeated interact requests cannot count the same client twice, and a disconnect can remove the
    // exact holder instead of guessing by decrementing a shared counter.
    public readonly HashSet<ulong> holdingClients = new HashSet<ulong>();
    public bool subscribedToDisconnects;
    public float lastLoggedRopeAmount = float.NaN;
    public float nextRopeLogTime;

    public void OnButtonInput()
    {
        controller.changeRopeRpc(!controller.isRopeFree.Value);
    }
    
    public void OnHandInput(float xValue, float yValue)
    {
        if(controller.isRopeFree.Value)
        {
            controller.pullAnchorRpc(Mathf.Abs(yValue));
            controller.pullAnchorRpc(Mathf.Abs(xValue));
        }
    }

    public void OnInteract(Player player)
    {
        controller.IncreaseHoldingPlayerRpc();
    }

    public void OnRightHandInput(float xValue, float yValue)
    {
    }

    public void OnUnInteract(Player player)
    {
        controller.DecreaseHoldingPlayerRpc();
    }

   


 
    public void HandleClientDisconnect(ulong clientId)
    {
        if (!controller.IsServer || !controller.IsSpawned) return;

        if (holdingClients.Remove(clientId))
        {
            SyncHoldingPlayerCount();
        }
    }

    public void SyncHoldingPlayerCount()
    {
        controller.holdingPlayerCount.Value = holdingClients.Count;
    }

    public void UnsubscribeFromDisconnects()
    {
        if (!subscribedToDisconnects) return;

        if (controller.NetworkManager != null)
        {
            controller.NetworkManager.OnClientDisconnectCallback -= HandleClientDisconnect;
        }

        subscribedToDisconnects = false;
    }

    public void LogRopeAmountIfNeeded()
    {
        if (!logRopeAmount) return;

        float currentAmount = controller.releasedRopeAmount.Value;
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

        if (!controller.IsServer) return;

        if (controller.holdingPlayerCount.Value == 0 && controller.isRopeFree.Value && controller.releasedRopeAmount.Value < maxRope)
        {
            controller.releasedRopeAmount.Value = Mathf.Min(
                maxRope,
                controller.releasedRopeAmount.Value + Time.deltaTime * ropeDescentMultiplier);
        }
    }
}
