using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;

public class WaterController : NetworkBehaviour
{
    public Material WaterMaterial;
    public NetworkVariable<Vector4> steepness;
    public NetworkVariable<Vector4> wavelength;
    public NetworkVariable<Vector4> speed;
    public NetworkVariable<Vector4> directions;


    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        WaterMaterial = GetComponent<Renderer>().sharedMaterial;
        foreach(GameObject obj in GameObject.FindGameObjectsWithTag("Water"))
        {
            obj.GetComponent<Renderer>().sharedMaterial = WaterMaterial;
        }
        if(!IsOwner)
            return;
        
    }


    // Update is called once per frame
    void Update()
    {
        if(IsOwner)
        {
            steepness.Value = WaterMaterial.GetVector("_Wave_Steepness");
            wavelength.Value = WaterMaterial.GetVector("_Wave_Length");
            speed.Value = WaterMaterial.GetVector("_Wave_Speed");
            directions.Value = WaterMaterial.GetVector("_Wave_Directions");
            WaterMaterial.SetFloat("_Wave_Time",(float)NetworkManager.Singleton.ServerTime.Time);

        }
        else
        {
            WaterMaterial.SetVector("_Wave_Steepness",steepness.Value);
            WaterMaterial.SetVector("_Wave_Length",wavelength.Value);
            WaterMaterial.SetVector("_Wave_Speed",speed.Value);
            WaterMaterial.SetVector("_Wave_Directions",directions.Value);
            WaterMaterial.SetFloat("_Wave_Time",(float)NetworkManager.Singleton.ServerTime.Time);

        }
       
    }
}
