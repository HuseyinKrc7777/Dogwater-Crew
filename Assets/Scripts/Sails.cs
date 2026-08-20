using System;
using System.Collections.Generic;
using Unity.Multiplayer.Tools.NetStats;
using Unity.Netcode;
using UnityEngine;

public class Sails : NetworkBehaviour
{
    public List<Sail> sailList;
    // rüzgar yönü dışarıdan değiştirilecek networkvariable olacak
    public Vector3 Wind = new Vector3();
    public NetworkList<float> SailRotations = new(writePerm:NetworkVariableWritePermission.Server,readPerm:NetworkVariableReadPermission.Everyone);
    public NetworkList<float> SailAreas = new(writePerm:NetworkVariableWritePermission.Server,readPerm:NetworkVariableReadPermission.Everyone);
    protected override void OnNetworkPostSpawn()
    {
        base.OnNetworkPostSpawn();
        foreach(Sail sail in sailList)
        {
            sail.index = sailList.IndexOf(sail);
            sail.sailController = this;
            SailRotations.Add(0);
            SailAreas.Add(1);

            if(IsServer)
            {
                sail.leftRope.controller.currentValueList.OnListChanged += sail.OnRotationRopeChanged;
                sail.rightRope.controller.currentValueList.OnListChanged += sail.OnRotationRopeChanged;
                sail.openRope.controller.currentValueList.OnListChanged += sail.OnOpenRopeChanged;
            }
           
            SailAreas.OnListChanged += sail.OnSailAreaChanged;
            SailRotations.OnListChanged += sail.OnSailRotationChanged;
        }

        Wind =WaterController.Instance.wind.Value;
        WaterController.Instance.wind.OnValueChanged+=OnWindChange;
    }
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
    }
    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        foreach(Sail sail in sailList)
        {
            if(IsServer)
            {
                sail.leftRope.controller.currentValueList.OnListChanged -= sail.OnRotationRopeChanged;
                sail.rightRope.controller.currentValueList.OnListChanged -= sail.OnRotationRopeChanged;
                sail.openRope.controller.currentValueList.OnListChanged -= sail.OnOpenRopeChanged;
            }
            SailAreas.OnListChanged -= sail.OnSailAreaChanged;
            SailRotations.OnListChanged -= sail.OnSailRotationChanged;
        }

        WaterController.Instance.wind.OnValueChanged-=OnWindChange;
    
    }
    public void OnWindChange(Vector3 oldValue , Vector3 newValue)
    {
        Wind = newValue;
    }
    public Vector3 GetTotalWindPush()
    {
        //yelken başına hesap yapılıp toplanılacak o geri gönderilecek
        Vector3 total = Vector3.zero;
        foreach(Sail sail in sailList)
        {
            total += sail.GetWindPush(Wind);
        }
        return total;
    }
    public Quaternion GetTotalWindRotation()
    {
        Quaternion total = Quaternion.identity;
        foreach(Sail sail in sailList)
        {
            total *= sail.GetWindRotation(Wind);
        }
        return total;
    }
}