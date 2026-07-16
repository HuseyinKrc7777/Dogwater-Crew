using Unity.Netcode;
using Unity.Services.Matchmaker.Models;
using UnityEngine;


public class ShipAi : NetworkBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    [SerializeField] Ship ship;
    void Start()
    {

    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if(!IsServer)
            return;
    }
    // Update is called once per frame
    void Update()
    {
        if(!IsServer)
            return;
        if(ship.whatKindOfShipIsThis != ShipKind.None && ship.whatKindOfShipIsThis != ShipKind.PlayerControlled)
        {
            Vector3 forw = WaterController.Instance.wind.Value.normalized;

            if(transform.parent!=null)
            {
                forw = transform.parent.InverseTransformDirection(forw);

                if(forw == Vector3.zero)
                {
                    forw = Vector3.forward;
                }
            }


        
            foreach(Sail sail in ship.sailList)
            {
                forw.y = 0;
                sail.transform.localRotation = Quaternion.LookRotation(forw);
            }
        }
    }
}
