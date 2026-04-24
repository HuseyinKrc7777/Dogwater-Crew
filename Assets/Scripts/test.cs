using Unity.Netcode;
using UnityEngine;

public class test : NetworkBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public Ship ship;
    //public CharacterController _controller;
    public Vector3 relativePos = Vector3.up * 3;
   
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        relativePos = Vector3.up * 3;
    }

    // Update is called once per frame
    private void Update() {
        if(!IsOwner)    return;
        transform.position = ship.transform.position + relativePos;
    }
}
