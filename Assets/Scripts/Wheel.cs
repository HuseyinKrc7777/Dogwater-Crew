using Unity.Mathematics;
using Unity.Netcode;
using UnityEngine;

public class Wheel : MonoBehaviour, IInteractable, IHandInput
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public WheelNetworkController controller;
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

        if (Mathf.Abs(currentZ - controller.rudderRotation.Value) > 0.1f)
        {
            float wheelZ = (controller.rudderRotation.Value / 45f) * 1080f;

            euler.z = wheelZ;
            transform.localEulerAngles = euler;
        }
    }
    
    public Vector3 GetRudderDirection()
    {
        float angle = Mathf.DeltaAngle(0f, controller.rudderRotation.Value) * -1f;
        angle = Mathf.Clamp(angle, -45f, 45f);

        Quaternion yaw = Quaternion.Euler(0f, angle, 0f);

        return yaw * transform.forward;
    }

    public void OnHandInput(float xValue, float yValue)
    {
        //TODO burda belki x ve y nin toplamı veya tutuş yerine göre x veya y daha ağırlıklı olacak şekilde olabilir
        //mesela önünden tutarsam sadece x , yanından tutarsan sadece y , ama yöne göre aynalanmış filan fişman öyle yani
        //belki olabilir ama gerek yok gibi bişey
        controller.RotateRudderRpc(yValue * 3);
    }

    public void OnUnInteract(Player player)
    {
        throw new System.NotImplementedException();
    }

    public void OnRightHandInput(float xValue, float yValue)
    {
        //test için yapılmıştır , tamamlanmış ürünü temsil etmemektedir
        controller.RotateRudderRpc(xValue);

    }

    public void OnButtonInput()
    {
        Debug.Log("Ben de eklendim evet");
        //throw new System.NotImplementedException();
    }
}
