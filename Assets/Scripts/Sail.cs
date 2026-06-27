using Unity.Netcode;
using UnityEngine;

public class Sail : NetworkBehaviour
{
    [Header("Sail Settings")]
    public float maxForce = 15f;
    public float liftCoefficient = 1.2f; // Efficiency of the "wing" effect

    public NetworkVariable<float> SailRotation = new NetworkVariable<float>(0f);

    public NetworkVariable<float> sailArea = new NetworkVariable<float>(1f); // 0 (closed) to 1 (full)

    public Rope rightRope;
    public Rope leftRope;
    public Rope openRope;


    private void OnEnable()
    {
        leftRope.currentValue.OnValueChanged += OnRopeChanged;
        rightRope.currentValue.OnValueChanged += OnRopeChanged;
        openRope.currentValue.OnValueChanged += OnSailAreaChanged;
        SailRotation.OnValueChanged += OnSailRotationChanged;
    }

    private void OnDisable()
    {
        leftRope.currentValue.OnValueChanged -= OnRopeChanged;
        rightRope.currentValue.OnValueChanged -= OnRopeChanged;
        openRope.currentValue.OnValueChanged -= OnSailAreaChanged;
        SailRotation.OnValueChanged -= OnSailRotationChanged;
    }
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        rightRope.hardMax = 80;
        rightRope.hardMin = -80;
        rightRope.minValue.Value = -80;

        //******************************************

        leftRope.hardMax = 80;
        leftRope.hardMin = -80;
        leftRope.minValue.Value = -80;

        //----------------------------------------------

        openRope.minValue.Value = 0;
        openRope.maxValue.Value = 100;

        openRope.currentValue.Value = 50;

        openRope.hardMax = 100;
        openRope.hardMin = 0;


        leftRope.currentValue.OnValueChanged += OnRopeChanged;
        rightRope.currentValue.OnValueChanged += OnRopeChanged;
        openRope.currentValue.OnValueChanged += OnSailAreaChanged;
        SailRotation.OnValueChanged += OnSailRotationChanged;

    }

    private void OnRopeChanged(float oldValue, float newValue)
    {
        if (!IsServer) return;

        rightRope.maxValue.Value = -leftRope.currentValue.Value;
        leftRope.maxValue.Value = -rightRope.currentValue.Value;

        if(rightRope.currentValue.Value > 0)
        {
            SailRotation.Value = rightRope.currentValue.Value;
        }
        else if(leftRope.currentValue.Value > 0)
        {
            SailRotation.Value = leftRope.currentValue.Value * -1;
            
        }
        
    }
    private void OnSailRotationChanged(float oldVal, float newVal)
    {
        Vector3 euler = transform.localEulerAngles;
        euler.y = SailRotation.Value;
        transform.localEulerAngles = euler;
    }
    private void OnSailAreaChanged(float oldVal, float newVal)
    {
        if(IsServer)
            sailArea.Value = openRope.currentValue.Value / 100;

        Vector3 scale = transform.localScale;
        scale.y = sailArea.Value;
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
        if (sailArea.Value <= 0.1f) return Vector3.zero;


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
        return transform.root.forward * Mathf.Max(0, forwardPush) * Mathf.Abs(wind.magnitude) * sailArea.Value * maxForce * tightnessMultiplier;
    }
    public Quaternion GetWindRotation(Vector3 wind)
    {
        float sideForce = Vector3.Dot(transform.root.right, wind.normalized);

        float heelDegrees = sideForce * wind.magnitude * sailArea.Value * 2.0f;

        return Quaternion.Euler(0, 0, -heelDegrees);
    }

    [Rpc(SendTo.Server)]
    public void UpdateSailAreaRpc(float value)
    {
        if (sailArea.Value + value > 1 || sailArea.Value + value < 0)
            return;
        sailArea.Value += value;
    }
    [Rpc(SendTo.Server)]
    public void UpdateSailDirectionRpc(float value)
    {
        if (Mathf.Abs(SailRotation.Value + value) > 80f)
        {
            return;
        }
        SailRotation.Value += value;
    }

}