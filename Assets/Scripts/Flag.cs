using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Networking;
public class WindFlag : NetworkBehaviour
{
    public Vector3 Wind;
    public GameObject Flag;
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        Wind = WaterController.Instance.wind.Value;
        WaterController.Instance.wind.OnValueChanged+=OnWindChange;
  
    }
    public void OnWindChange(Vector3 oldValue , Vector3 newValue)
    {
        Wind = newValue;
    }
    void Update()
    {
        Vector3 forw = Wind.normalized;
        if(transform.parent!=null)
        {
            forw = transform.parent.InverseTransformDirection(forw);
            if(forw == Vector3.zero)
            {
                forw = Vector3.down;
            }
        }
        Flag.transform.localRotation = Quaternion.LookRotation(forw);
        
    }
}
