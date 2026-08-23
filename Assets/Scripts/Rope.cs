using Unity.Netcode;
using UnityEngine;

public class Rope : MonoBehaviour , IInteractable , IHandInput
{
    public int index;
    public RopeNetworkController controller;
    public float startValue = 0;
    public float startMaxValue = 0;
    public float startMinValue = 0;
    
    
    public float hardMax = 80;
    public float hardMin = -80;


    public void OnHandInput(float xValue , float yValue)
    {
        float value = yValue;

        if(controller.currentValueList[index] + value > hardMax || controller.currentValueList[index] - value < hardMin || controller.currentValueList[index] + value > controller.maxValueList[index] | controller.currentValueList[index] - value < controller.minValueList[index])
            return;
        controller.changeValueRpc(index,value);
    }
    public float GetCurrentValue()
    {
        return controller.currentValueList[index];
    }
    public float GetMaxValue()
    {
        return controller.maxValueList[index];
    }
    public float GetMinValue()
    {
        return controller.minValueList[index];
    }
    public void SetCurrentValue(float value)
    {
        controller.currentValueList[index] = value;
    }
    public void SetMaxValue(float value)
    {
        controller.maxValueList[index] = value;
    }
    public void SetMinValue(float value)
    {
        controller.minValueList[index] = value;
    }

    public void OnInteract(Player player)
    {
        //throw new System.NotImplementedException();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void OnUnInteract(Player player)
    {
        //throw new System.NotImplementedException();
    }

    public void OnRightHandInput(float xValue, float yValue)
    {
        //throw new System.NotImplementedException();
    }

    public void OnButtonInput()
    {
        //throw new System.NotImplementedException();
    }
}
