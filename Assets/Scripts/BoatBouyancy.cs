using Unity.Netcode;
using UnityEngine;

public class BoatMovement : NetworkBehaviour
{
    [Header("Waves")]
    [SerializeField] Vector4 steepness;
    [SerializeField] Vector4 wavelength;
    [SerializeField] Vector4 speed;
    [SerializeField] Vector4 directions;
    [SerializeField] Sails sails;

    [Header("Buoyancy")]
    public float strength = 1f;
    public float objectDepth = 1f;

    [Header("Speed Boat Physics")]
    [Tooltip("How much the boat rises out of the water based on speed.")]
    public float planingStrength = 0.2f; 
    [Tooltip("Maximum height the boat can lift above the water surface.")]
    public float maxPlaningLift = 0.8f;
    [Tooltip("Limits how much the boat can pitch up/down (X-axis) to prevent flipping.")]
    public float maxPitchAngle = 15f;

    [Header("Effectors")]
    public Transform[] effectors;

    [Header("Smoothing")]
    public float positionLerp = 3f;
    public float rotationLerp = 3f;
    [Header("Advanced Tuning")]
    public float minRotationMultiplier = 0.3f; 
    public float highSpeedThreshold = 20f;

    private Vector3[] effectorTargets;
    private Vector3 velocity;

    private WaterController waterController;
    private Rigidbody rb;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (!IsOwner) return;
        rb = GetComponent<Rigidbody>();
        effectorTargets = new Vector3[effectors.Length];
        waterController = GameObject.FindGameObjectWithTag("WaterController").GetComponent<WaterController>();
    }

    void FixedUpdate()
    {
        if (!IsOwner) return;

        // Get synced wave params
        steepness = waterController.steepness.Value;
        wavelength = waterController.wavelength.Value;
        speed = waterController.speed.Value;
        directions = waterController.directions.Value;

        Vector3 center = Vector3.zero;
        float totalWeight = 0f;
        int count = effectors.Length;

        // --- SAMPLE WAVES ---
        Vector3 wave = Vector3.zero;
        for (int i = 0; i < count; i++)
        {
            Vector3 p = effectors[i].position;
            wave = GerstnerWaveDisplacement.GetWaveDisplacement(
                p,
                new float[] { steepness.x, steepness.y, steepness.z, steepness.w },
                new float[] { wavelength.x, wavelength.y, wavelength.z, wavelength.w },
                new float[] { speed.x, speed.y, speed.z, speed.w },
                new float[] { directions.x, directions.y, directions.z, directions.w }
            );

            effectorTargets[i] = new Vector3(p.x, wave.y, p.z);

            // SPEED BOAT TWEAK: Weight rear effectors more to keep the engine in the water 
            // and the bow (front) light for jumping.
            float weight = (i < count / 2) ? 1.5f : 1.0f; // Assuming 0,1 are back, 2,3 are front
            center += effectorTargets[i] * weight;
            totalWeight += weight;
        }

        if (totalWeight > 0f) center /= totalWeight;

        // --- NORMAL ---
        Vector3 normal = Vector3.up;
        if (count >= 3)
        {
            Vector3 a = effectorTargets[0];
            Vector3 b = effectorTargets[1];
            Vector3 c = effectorTargets[2];
            normal = Vector3.Cross(b - a, c - a).normalized;
            if (normal == Vector3.zero || float.IsNaN(normal.x)) normal = Vector3.up;
        }

        // --- SPEED BOAT LIFT (PLANING) ---
        // Calculate forward speed. Dot product ensures we only care about moving forward.
        Vector3 currentVelocity = rb.linearVelocity;
        if (float.IsNaN(currentVelocity.x) || float.IsNaN(currentVelocity.y) || float.IsNaN(currentVelocity.z))
        {
            currentVelocity = Vector3.zero;
            rb.linearVelocity = currentVelocity;
        }
        float forwardSpeed = Vector3.Dot(currentVelocity, transform.forward);
        float dynamicLift = Mathf.Clamp(forwardSpeed * planingStrength, 0, maxPlaningLift);

        // --- POSITION ---
        // Reduce objectDepth by dynamicLift to make the boat sit "higher"
        float currentBuoyancyOffset = objectDepth - dynamicLift;

        Vector3 currentPos = rb.position;
        if (float.IsNaN(currentPos.x) || float.IsNaN(currentPos.y) || float.IsNaN(currentPos.z))
        {
            currentPos = transform.position; // Fallback
        }

        Vector3 targetPos = new Vector3(
            currentPos.x,
            center.y - currentBuoyancyOffset,
            currentPos.z
        );

        targetPos += WaveDrift(wave) * Time.fixedDeltaTime;
        targetPos += sails.GetTotalWindPush() * Time.fixedDeltaTime;

        if (float.IsNaN(velocity.x) || float.IsNaN(velocity.y) || float.IsNaN(velocity.z) || float.IsInfinity(velocity.y)) 
        {
            velocity = Vector3.zero;
        }

        // --- JUMP LOGIC (SMOOTHING BIAS) ---
        float verticalDiff = targetPos.y - currentPos.y;
        float adjustedLerp = positionLerp;

        // If boat is moving fast and the water drops (verticalDiff < 0), 
        // slow down the "pull" to create air-time/jumping effect.
        if (forwardSpeed > 5f && verticalDiff < 0)
        {
            adjustedLerp *= 0.3f; // Less aggressive following of the water's downward slope
        }

        Vector3 newPos = Vector3.SmoothDamp(
            currentPos,
            targetPos,
            ref velocity,
            1f / adjustedLerp,
            Mathf.Infinity,
            Time.fixedDeltaTime
        );

        if (!float.IsNaN(newPos.x) && !float.IsNaN(newPos.y) && !float.IsNaN(newPos.z))
        {
            rb.MovePosition(newPos);
        }

        // --- ROTATION ---
        Quaternion currentRot = rb.rotation;
        if (float.IsNaN(currentRot.x) || float.IsNaN(currentRot.y) || float.IsNaN(currentRot.z) || float.IsNaN(currentRot.w) || currentRot == new Quaternion(0,0,0,0))
        {
            currentRot = Quaternion.identity;
        }

        Quaternion targetRot = Quaternion.FromToRotation(transform.up, normal) * currentRot;
        targetRot *= WaveRotation(wave);
        targetRot *= sails.GetTotalWindRotation();

        // SPEED BOAT TWEAK: Clamp Pitch to stop the boat from flipping vertically
        Vector3 angles = targetRot.eulerAngles;
        float pitch = angles.x;
        if (pitch > 180) pitch -= 360;
        pitch = Mathf.Clamp(pitch, -maxPitchAngle, maxPitchAngle);
        targetRot = Quaternion.Euler(pitch, angles.y, angles.z);

        float speedFactor = Mathf.Clamp01(forwardSpeed / highSpeedThreshold);

        float currentRotLerp = Mathf.Lerp(rotationLerp, rotationLerp * minRotationMultiplier, speedFactor);

        Quaternion newRot = Quaternion.Slerp(
            currentRot,
            targetRot,
            currentRotLerp * Time.fixedDeltaTime
        );
        
        if (!float.IsNaN(newRot.x) && !float.IsNaN(newRot.y) && !float.IsNaN(newRot.z) && !float.IsNaN(newRot.w) && newRot != new Quaternion(0,0,0,0))
        {
            rb.MoveRotation(newRot);
        }

    }

    public float driftIntensity = 0.09f; 
    private Vector3 WaveDrift(Vector3 wave) => new Vector3(wave.x, 0, wave.z) * driftIntensity;

    public float driftRotationIntensiy = 0.00001f;
    private Quaternion WaveRotation(Vector3 wave)
    {
        float driftAngle = Mathf.Atan2(wave.x, wave.z) * Mathf.Rad2Deg;
        float subtleYaw = driftAngle * driftRotationIntensiy;
        return Quaternion.Euler(0, subtleYaw, 0);
    }
}