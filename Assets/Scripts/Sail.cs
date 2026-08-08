using System;
using Unity.Netcode;
using UnityEngine;

public class Sail : MonoBehaviour
{
    [Header("Sail Settings")]
    public float maxForce = 15f;
    public float liftCoefficient = 1.2f; // Efficiency of the "wing" effect
    public int index;
    public Sails sailController;
    
    public Rope rightRope;
    public Rope leftRope;
    public Rope openRope;

    public void OnRotationRopeChanged(float oldValue, float newValue)
    {

        rightRope.maxValue.Value = -leftRope.currentValue.Value;
        leftRope.maxValue.Value = -rightRope.currentValue.Value;

        if(rightRope.currentValue.Value > 0)
        {
            sailController.SailRotations[index] = rightRope.currentValue.Value;
        }
        else if(leftRope.currentValue.Value > 0)
        {
            sailController.SailRotations[index] = leftRope.currentValue.Value * -1;
            
        }
        
    }
    public void OnSailRotationChanged(NetworkListEvent<float> changeEvent)
    {
        Vector3 euler = transform.localEulerAngles;
        euler.y = sailController.SailRotations[index];
        transform.localEulerAngles = euler;
    }
    public void OnOpenRopeChanged(float oldVal, float newVal)
    {
        sailController.SailAreas[index] = openRope.currentValue.Value / 100;
    }
    public void OnSailAreaChanged(NetworkListEvent<float> changeEvent)
    {
        Vector3 scale = transform.localScale;
        scale.y = sailController.SailAreas[index];
        transform.localScale = scale;
    }

    private float _getTightness()
    {
        return rightRope.currentValue.Value + leftRope.currentValue.Value;
    }
    void Update()
    {
     
    }
    public Vector3 GetSailDirection()
    {
        //return transform.TransformDirection(transform.forward);
        return transform.forward;
    }
    public Vector3 GetWindPush(Vector3 wind)
    {
        if (sailController.SailAreas[index] <= 0.1f) return Vector3.zero;


        Vector3 currentSailForward = GetSailDirection();

        float angleOfAttack = Vector3.Angle(currentSailForward, wind.normalized);

        if(angleOfAttack > 100)
            return Vector3.zero;
        if(angleOfAttack > 35)
            angleOfAttack =  35 + (angleOfAttack-35) / 2f;
        float lift = Mathf.Cos(angleOfAttack ) * liftCoefficient;
        float drag = Mathf.Max(0, Vector3.Dot(currentSailForward, wind.normalized)) * 0.5f;

        Vector3 sailNormal = Vector3.Cross(currentSailForward, Vector3.up);
        Vector3 totalForceVector = sailNormal * lift + currentSailForward * drag;

        float forwardPush = Vector3.Dot(totalForceVector, transform.root.forward);

        float tightnessMultiplier = (100 - Mathf.Abs(_getTightness())) / 100;
        return transform.root.forward * Mathf.Max(0, forwardPush) * Mathf.Abs(wind.magnitude) * sailController.SailAreas[index] * maxForce * tightnessMultiplier;
    }
    public Quaternion GetWindRotation(Vector3 wind)
    {
        float sideForce = Vector3.Dot(transform.root.right, wind.normalized);

        float heelDegrees = sideForce * wind.magnitude * sailController.SailAreas[index] * 2.0f;

        return Quaternion.Euler(0, 0, -heelDegrees);
    }

    [Rpc(SendTo.Server)]
    public void UpdateSailAreaRpc(float value)
    {
        if (sailController.SailAreas[index] + value > 1 || sailController.SailAreas[index] + value < 0)
            return;
        sailController.SailAreas[index] += value;
    }
    [Rpc(SendTo.Server)]
    public void UpdateSailDirectionRpc(float value)
    {
        if (Mathf.Abs(sailController.SailRotations[index] + value) > 80f)
        {
            return;
        }
        sailController.SailRotations[index] += value;
    }

    internal void OnSailRotationChangedd(NetworkListEvent<float> changeEvent)
    {
        throw new NotImplementedException();
    }
}