using Unity.Mathematics;
using Unity.Netcode;
using UnityEngine;

public class Wheel : NetworkBehaviour, IInteractable, IHandInput
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public NetworkVariable<float> rudderRotation = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public void OnInteract(Player player)
    {
        //        throw new System.NotImplementedException();
    }

    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        Vector3 euler = transform.localEulerAngles;

        // convert to -180 → 180 range
        float currentZ = Mathf.DeltaAngle(0f, euler.z);

        if (Mathf.Abs(currentZ - rudderRotation.Value) > 0.1f)
        {
            float wheelZ = (rudderRotation.Value / 45f) * 1080f;

            euler.z = wheelZ;
            transform.localEulerAngles = euler;
        }
    }
    [Rpc(SendTo.Server)]
    private void RotateRudderRpc(float rotation)
    {
        if(Mathf.Abs( rudderRotation.Value + rotation) > 45f)
        {
            return;
        }
        rudderRotation.Value += rotation;
    }
    public Vector3 GetRudderDirection()
    {
        float angle = Mathf.DeltaAngle(0f, rudderRotation.Value) * -1f;
        angle = Mathf.Clamp(angle, -45f, 45f);

        Quaternion yaw = Quaternion.Euler(0f, angle, 0f);

        return yaw * transform.forward;
    }

    public void OnHandInput(float xValue, float yValue)
    {
        //TODO burda belki x ve y nin toplamı veya tutuş yerine göre x veya y daha ağırlıklı olacak şekilde olabilir
        //mesela önünden tutarsam sadece x , yanından tutarsan sadece y , ama yöne göre aynalanmış filan fişman öyle yani
        //belki olabilir ama gerek yok gibi bişey
        RotateRudderRpc(yValue);
    }

    public void OnUnInteract(Player player)
    {
        throw new System.NotImplementedException();
    }
}
