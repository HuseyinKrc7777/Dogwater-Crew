using Unity.Netcode;
using UnityEngine;

public class Sail : NetworkBehaviour
{
    [Header("Sail Settings")]
    public float maxForce = 15f;
    public float liftCoefficient = 1.2f; // Efficiency of the "wing" effect

    // Synced variables
    // bu da networkvariable olacak
    public Vector3 sailDirection = Vector3.forward;
    public NetworkVariable<float> sailArea = new NetworkVariable<float>(1f); // 0 (closed) to 1 (full)

    public Vector3 GetWindPush(Vector3 wind)
    {
        if (sailArea.Value <= 0.1f) return Vector3.zero;


        Vector3 currentSailForward = transform.TransformDirection(sailDirection);

        float angleOfAttack = Vector3.Angle(currentSailForward, wind.normalized);


        float lift = Mathf.Sin(angleOfAttack * Mathf.Deg2Rad * 2) * liftCoefficient;
        float drag = Mathf.Max(0, Vector3.Dot(currentSailForward, wind.normalized)) * 0.5f;

       
        Vector3 sailNormal = Vector3.Cross(currentSailForward, Vector3.up);
        Vector3 totalForceVector = sailNormal * lift + currentSailForward * drag;

        float forwardPush = Vector3.Dot(totalForceVector, transform.root.forward);

        return transform.root.forward * Mathf.Max(0, forwardPush) * wind.magnitude * sailArea.Value * maxForce;
    }
    public Quaternion GetWindRotation(Vector3 wind)
{
    float sideForce = Vector3.Dot(transform.root.right, wind.normalized);
    
    float heelDegrees = sideForce * wind.magnitude * sailArea.Value * 2.0f;
    
    return Quaternion.Euler(0, 0, -heelDegrees);
}

    [Rpc(SendTo.Server)]
    public void UpdateSailAreaRpc(float newArea)
    {
        sailArea.Value = Mathf.Clamp01(newArea);
    }
}