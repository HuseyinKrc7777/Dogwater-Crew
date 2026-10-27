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

    public void OnRotationRopeChanged(NetworkListEvent<float> changeEvent)
    {
        //TODO bu ve alttaki fonksiyonlarda başka yelkendeki değer değişse de bütün yelkenleri senkronize ediyor , sonradan düzeltilebilir.
        rightRope.SetMaxValue(-leftRope.GetCurrentValue()); 
        leftRope.SetMaxValue(-rightRope.GetCurrentValue());

        if(rightRope.GetCurrentValue() > 0)
        {
            sailController.SailRotations[index] = rightRope.GetCurrentValue();
        }
        else if(leftRope.GetCurrentValue() > 0)
        {
            sailController.SailRotations[index] = leftRope.GetCurrentValue() * -1;
            
        }
        
    }
    public void OnSailRotationChanged(NetworkListEvent<float> changeEvent)
    {
        
        Vector3 euler = transform.localEulerAngles;
        euler.y = sailController.SailRotations[index];
        transform.localEulerAngles = euler;
    }
    public void OnOpenRopeChanged(NetworkListEvent<float> changeEvent)
    {

        
        sailController.SailAreas[index] = openRope.GetCurrentValue() / 100;
    }
    public void OnSailAreaChanged(NetworkListEvent<float> changeEvent)
    {
        
        Vector3 scale = transform.localScale;
        scale.y = sailController.SailAreas[index];
        transform.localScale = scale;
    }

    private float _getTightness()
    {
        return rightRope.GetCurrentValue() + leftRope.GetCurrentValue();
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
        // Fix B (agreed with Hüseyin, 2026-09-23). Old lines kept for reference:
        //float lift = Mathf.Cos(angleOfAttack ) * liftCoefficient;
        //   -> Vector3.Angle returns degrees but Mathf.Cos expects radians, so lift jumped around
        //      (10° gave -0.84, 20° gave +0.41) whenever the wind direction changed a little.
        float lift = Mathf.Cos(angleOfAttack * Mathf.Deg2Rad) * liftCoefficient;
        float drag = Mathf.Max(0, Vector3.Dot(currentSailForward, wind.normalized)) * 0.5f;

        Vector3 sailNormal = Vector3.Cross(currentSailForward, Vector3.up);
        // Old: sailNormal always pointed to the same side of the sail, so turning the sail one way always
        // helped and the other way always hurt, whichever side the wind came from. Lift pushes the sail
        // toward the downwind side, so flip the normal to the side the wind is blowing toward.
        if (Vector3.Dot(sailNormal, wind.normalized) < 0f)
            sailNormal = -sailNormal;
        Vector3 totalForceVector = sailNormal * lift + currentSailForward * drag;

        float forwardPush = Vector3.Dot(totalForceVector, transform.root.forward);

        float tightnessMultiplier = (100 - Mathf.Abs(_getTightness())) / 100;
        return transform.root.forward * Mathf.Max(0.1f, forwardPush) * Mathf.Abs(wind.magnitude) * sailController.SailAreas[index] * maxForce * tightnessMultiplier;
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
}