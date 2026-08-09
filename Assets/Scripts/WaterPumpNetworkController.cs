using Unity.Netcode;
using UnityEngine;

public class WaterPumpNetworkController : NetworkBehaviour
{
    public WaterPump pump;
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        pump.controller = this;
    }
    [Rpc(SendTo.Server)]
    public void PumpWaterRpc(float value)
    {
        if(pump.ship.waterInsideTheShip.Value > 0)

        pump.ship.waterInsideTheShip.Value -= Mathf.Abs(value);
    }

}