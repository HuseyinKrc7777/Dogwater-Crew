using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class RopeNetworkController : NetworkBehaviour
{
    public List<Rope> ropes = new();
    public NetworkList<float> currentValueList = new();
    public NetworkList<float> maxValueList = new();
    public NetworkList<float> minValueList = new();

 
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        foreach(Rope rope in ropes)
        {
            rope.index = ropes.IndexOf(rope);
            rope.controller = this;
            currentValueList.Add(rope.startValue);
            maxValueList.Add(rope.startMaxValue);
            minValueList.Add(rope.startMinValue);
        }

    }

    [Rpc(SendTo.Server)]
    public void changeValueRpc(int index,float value)
    {
        currentValueList[index] += value;
    }

    public void OnInteract(Player player)
    {
        //throw new System.NotImplementedException();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
  

    // Update is called once per frame
    void Update()
    {
        
    }

    
}
