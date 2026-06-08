using Unity.Netcode;
using UnityEngine;

public class Anchor : NetworkBehaviour, IInteractable, IHandInput
{
    public NetworkVariable<float> releasedRopeAmount = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> isRopeFree = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> holdingPlayerCount = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    
    public float maxRope = 100;
    public float ropeDescentMultiplier = 10f;
    public void OnButtonInput()
    {
        changeRopeRpc(!isRopeFree.Value);
    }
    [Rpc(SendTo.Server)]
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
    [Rpc(SendTo.Server)]
    public void pullAnchorRpc(float value)
    {
        if(releasedRopeAmount.Value > 0) 
            releasedRopeAmount.Value -= value;
    }
    [Rpc(SendTo.Server)]
    public void IncreaseHoldingPlayerRpc()
    {
        holdingPlayerCount.Value++;
    }
    [Rpc(SendTo.Server)]
    public void DecreaseHoldingPlayerRpc()
    {
        holdingPlayerCount.Value--;
    }

    public void OnInteract(Player player)
    {
        IncreaseHoldingPlayerRpc();
    }

    public void OnRightHandInput(float xValue, float yValue)
    {
        throw new System.NotImplementedException();
    }

    public void OnUnInteract(Player player)
    {
        DecreaseHoldingPlayerRpc();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
    }

    // Update is called once per frame
    void Update()
    {
        if(!IsServer)
            return;
        if(holdingPlayerCount.Value == 0 && isRopeFree.Value && releasedRopeAmount.Value < maxRope)
        {
            releasedRopeAmount.Value += Time.deltaTime * ropeDescentMultiplier;
        }
    }
}
