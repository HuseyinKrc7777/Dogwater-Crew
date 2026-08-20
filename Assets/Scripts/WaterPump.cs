using System;
using Unity.Mathematics;
using Unity.Netcode;
using UnityEngine;

public class WaterPump : MonoBehaviour, IInteractable, IHandInput
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    [SerializeField] public Ship ship;
    public WaterPumpNetworkController controller;
    public void OnInteract(Player player)
    {
        //        throw new System.NotImplementedException();
    }

 
    // Update is called once per frame
    void Update()
    {
       
    }
    
    public void OnHandInput(float xValue, float yValue)
    {
        if(yValue > 0)
            controller.PumpWaterRpc(yValue);
    }

    public void OnUnInteract(Player player)
    {
        //throw new System.NotImplementedException();
    }

    public void OnRightHandInput(float xValue, float yValue)
    {
        //test için yapılmıştır , tamamlanmış ürünü temsil etmemektedir

    }

    public void OnButtonInput()
    {
        Debug.Log("Ben de eklendim evet");
        //throw new System.NotImplementedException();
    }
}
