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
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        Wind = GameObject.FindGameObjectWithTag("WaterController").GetComponent<WaterController>().wind.Value;
        GameObject.FindGameObjectWithTag("WaterController").GetComponent<WaterController>().wind.OnValueChanged+=OnWindChange;
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