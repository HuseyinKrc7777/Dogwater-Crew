using Unity.Netcode;
using UnityEngine;

public class WheelNetworkController :NetworkBehaviour
{
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        wheel.controller = this;
    }
    public Wheel wheel;
    public NetworkVariable<float> rudderRotation = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Rpc(SendTo.Server)]
    public void RotateRudderRpc(float rotation)
    {
        if(Mathf.Abs( rudderRotation.Value + rotation) > 45f)
        {
            return;
        }
        rudderRotation.Value += rotation;
    }
}