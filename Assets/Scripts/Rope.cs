using Unity.Netcode;
using UnityEngine;

public class Rope : NetworkBehaviour , IInteractable , IHandInput
{
    public NetworkVariable<float> currentValue = new NetworkVariable<float>(0f);
    public NetworkVariable<float> maxValue = new NetworkVariable<float>(0f);
    public NetworkVariable<float> minValue = new NetworkVariable<float>(0f);

    public float hardMax = 80;
    public float hardMin = -80;


    public void OnHandInput(float value)
    {
        changeValueRpc(value);
    }

    [Rpc(SendTo.Server)]
    private void changeValueRpc(float value)
    {
        if(currentValue.Value + value > hardMax || currentValue.Value - value < hardMin || currentValue.Value + value > maxValue.Value | currentValue.Value - value < minValue.Value)
            return;
        currentValue.Value += value;
    }

    public void OnInteract()
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
}
