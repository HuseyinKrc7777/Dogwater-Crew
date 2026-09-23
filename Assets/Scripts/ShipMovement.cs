using Unity.Netcode;
using UnityEngine;

public class BoatMovement : NetworkBehaviour
{
    [Header("Waves")]
    [SerializeField] Sails sails;

    [Header("Buoyancy")]
    public float strength = 1f;
    public float objectDepth = 1f;

    

    [Header("Effectors")]
    public Transform[] effectors;

    [Header("Smoothing")]
    public float positionLerp = 3f;
    public float rotationLerp = 3f;
    [Header("Advanced Tuning")]
    public float minRotationMultiplier = 0.3f;
    public float highSpeedThreshold = 5f;

    private Vector3[] effectorTargets;
    private Vector3 velocity;

    private WaterController waterController;
    private Rigidbody rb;
    private Ship ship;
    public Vector3 currentVelocity;
    public Vector3 currentAngularVelocity;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (!IsOwner) return;
        rb = GetComponent<Rigidbody>();
        effectorTargets = new Vector3[effectors.Length];
        waterController = WaterController.Instance;
        ship = GetComponent<Ship>();
    }

    void FixedUpdate()
    {
        if (!IsOwner) return;

        
        Vector3 center = Vector3.zero;
        float totalWeight = 0f;
        int count = effectors.Length;

        // --- SAMPLE WAVES ---
        Vector3 wave = Vector3.zero;
        for (int i = 0; i < count; i++)
        {
            Vector3 p = effectors[i].position;
            
            Vector3? temp = WaterController.Instance.GetWave(p);
            if(temp!=null)
                wave = (Vector3)temp;
            else
                wave = p;

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
        currentVelocity = rb.linearVelocity;
        if (float.IsNaN(currentVelocity.x) || float.IsNaN(currentVelocity.y) || float.IsNaN(currentVelocity.z))
        {
            currentVelocity = Vector3.zero;
            rb.linearVelocity = currentVelocity;
        }
        float forwardSpeed = Vector3.Dot(currentVelocity, transform.forward);

        // --- POSITION ---
        float currentBuoyancyOffset = objectDepth;

        Vector3 currentPos = rb.position;
        if (float.IsNaN(currentPos.x) || float.IsNaN(currentPos.y) || float.IsNaN(currentPos.z))
        {
            currentPos = transform.position; 
        }

        Vector3 targetPos = new Vector3(
            currentPos.x,
            Mathf.Lerp(currentPos.y,center.y - currentBuoyancyOffset,0.2f) ,
            currentPos.z
        );

        // Disabled: since the HDRP switch GetWave returns the projected world POSITION, not a wave
        // displacement, so this pushed the ship away from world origin in proportion to its distance
        // from it - speed kept growing the further the ship sailed. Replaced by the ocean current below.
        //targetPos += WaveDrift(wave) * Time.fixedDeltaTime;
        targetPos += sails.GetTotalWindPush() * Time.fixedDeltaTime;

        if (float.IsNaN(velocity.x) || float.IsNaN(velocity.y) || float.IsNaN(velocity.z) || float.IsInfinity(velocity.y))
        {
            velocity = Vector3.zero;
        }

     
        Vector3 desiredDirection = (targetPos - currentPos);

        if (desiredDirection.magnitude > 0.001f)
        {
            desiredDirection.Normalize();
        }


        float acceleration = 0.40f;
        float deceleration = 0.20f;

        Vector3 desiredVelocity = desiredDirection * (targetPos - currentPos).magnitude * 10;

        // Ocean current: the sea carries the ship even with sails furled. Added as a velocity, so it does
        // not depend on where the ship is. Before the anchor check below, so a fully anchored ship holds.
        if (WaterController.Instance != null)
        {
            desiredVelocity += WaterController.Instance.current.Value;
        }

        float accelRate =
            desiredVelocity.magnitude > currentVelocity.magnitude
            ? acceleration
            : deceleration;
        
        //buradaki kontrol değeri , konumun derinliği ile alaklıdır , bölgeden bölgeye değişecektir.
        if(ship.anchor.IsFullyDeployed)
        {
            desiredVelocity  = Vector3.zero;
        }
        else if (ship.anchor.controller.releasedRopeAmount.Value > 1)
        {
            accelRate -= accelRate / 4;
        }

        currentVelocity = Vector3.MoveTowards(
            currentVelocity,
            desiredVelocity,
            accelRate * Time.fixedDeltaTime
        );

        Vector3 newPos =
            currentPos +
            currentVelocity * Time.fixedDeltaTime;

        newPos = Vector3.Scale(new Vector3(1, 0, 1), newPos);

        newPos = new Vector3(
            newPos.x,
            center.y - currentBuoyancyOffset,
            newPos.z
        );

        rb.MovePosition(newPos);

        Quaternion currentRot = rb.rotation;

        Quaternion waveRot = Quaternion.FromToRotation(transform.up, normal) * currentRot;

        Vector3 waveAngles = waveRot.eulerAngles;
        waveRot = Quaternion.Euler(waveAngles.x, currentRot.eulerAngles.y, waveAngles.z);


        Vector3 rudderDir = ship.wheel.GetRudderDirection();

        Vector3 flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 flatRudder = Vector3.ProjectOnPlane(rudderDir, Vector3.up).normalized;

        float rudderAngle = Vector3.SignedAngle(flatForward, flatRudder, Vector3.up);


        float speedFactor = 1; //Mathf.Clamp01(forwardSpeed / highSpeedThreshold);

        float turnSpeed = 10f;


        float yawDelta = rudderAngle * speedFactor * turnSpeed * Time.fixedDeltaTime;

        float angularDamping = 0.2f; 

        yawDelta *= Mathf.Clamp01(forwardSpeed / highSpeedThreshold);
        yawDelta = Mathf.Lerp(yawDelta, 0f, angularDamping * Time.fixedDeltaTime);

        float minTurnSpeed = 0.3f;

        if (forwardSpeed < minTurnSpeed)
        {
            yawDelta = 0.1f;
        }

        Quaternion yawRot = Quaternion.Euler(0f, yawDelta, 0f);


        Quaternion targetRot = yawRot * waveRot;

        float angularAccel = 60f;

        Quaternion delta =
            targetRot * Quaternion.Inverse(currentRot);

        delta.ToAngleAxis(out float angle, out Vector3 axis);

        if (angle > 180f)
        {
            angle -= 360f;
        }

        if (Mathf.Abs(angle) > 0.01f)
        {
            Vector3 desiredAngularVelocity =
                axis.normalized *
                angle *
                Mathf.Deg2Rad;

            currentAngularVelocity = Vector3.MoveTowards(
                currentAngularVelocity,
                desiredAngularVelocity,
                angularAccel * Mathf.Deg2Rad * Time.fixedDeltaTime
            );

            Vector3 currentEuler = currentRot.eulerAngles;


            Vector3 waveEuler = waveRot.eulerAngles;

            float targetPitch = waveEuler.x;
            float targetRoll = waveEuler.z;

            float newPitch = Mathf.LerpAngle(
                currentEuler.x,
                targetPitch,
                rotationLerp * Time.fixedDeltaTime
            );

            float newRoll = Mathf.LerpAngle(
                currentEuler.z,
                targetRoll,
                rotationLerp * Time.fixedDeltaTime
            );


            float currentYaw = currentEuler.y;


            float desiredYawVelocity =
                yawDelta * Mathf.Deg2Rad;


            currentAngularVelocity.y = Mathf.MoveTowards(
                currentAngularVelocity.y,
                desiredYawVelocity,
                angularAccel * Time.fixedDeltaTime
            );

            float newYaw =
                currentYaw +
                currentAngularVelocity.y *
                Mathf.Rad2Deg *
                Time.fixedDeltaTime;

            Quaternion finalRot = Quaternion.Euler(
                newPitch,
                newYaw,
                newRoll
            );

            if (!float.IsNaN(finalRot.x) &&
                !float.IsNaN(finalRot.y) &&
                !float.IsNaN(finalRot.z) &&
                !float.IsNaN(finalRot.w) &&
                finalRot != new Quaternion(0, 0, 0, 0))
            {
                rb.MoveRotation(finalRot);
            }


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
