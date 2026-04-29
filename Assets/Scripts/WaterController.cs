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
    public NetworkVariable<Vector3> wind = new NetworkVariable<Vector3>(Vector3.forward, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public Vector3 editorWind;
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        WaterMaterial = GetComponent<Renderer>().sharedMaterial;
        foreach(GameObject obj in GameObject.FindGameObjectsWithTag("Water"))
        {
            obj.GetComponent<Renderer>().sharedMaterial = WaterMaterial;
        }
        if (IsServer)
        {
            wind.Value = Vector3.forward;
        }
        if(!IsOwner)
            return;
    }

    // Update is called once per frame
    void Update()
    {
        if(IsServer)
        {
            wind.Value = editorWind;
        }
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
