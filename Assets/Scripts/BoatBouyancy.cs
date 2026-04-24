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
    [Header("Effectors")]
    public Transform[] effectors;

    private WaterController waterController;
    private Rigidbody rb;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (!IsOwner) return;
        rb = GetComponent<Rigidbody>();
        waterController = GameObject.FindGameObjectWithTag("WaterController").GetComponent<WaterController>();
    }
     public float velocityDrag = 0.99f;
    public float angularDrag = 0.5f;

    void FixedUpdate()
    {
        if (!IsOwner) return;

        // Get synced wave params
        steepness = waterController.steepness.Value;
        wavelength = waterController.wavelength.Value;
        speed = waterController.speed.Value;
        directions = waterController.directions.Value;

        Vector3 center = Vector3.zero;
        int count = effectors.Length;

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

            rb.AddForceAtPosition(Physics.gravity / count, p, ForceMode.Force);

            
            var waveHeight = wave.y;
            var effectorHeight = p.y;

            if (!(effectorHeight < waveHeight)) continue; // submerged

            var submersion = Mathf.Clamp01(waveHeight - effectorHeight) / objectDepth;
            var buoyancy = Mathf.Abs(Physics.gravity.y) * submersion * strength;

            // buoyancy
            rb.AddForceAtPosition(Vector3.up * buoyancy, p, ForceMode.Force);

            // drag
            rb.AddForce(-rb.linearVelocity * (velocityDrag * Time.fixedDeltaTime), ForceMode.VelocityChange);

            // torque
            rb.AddTorque(-rb.angularVelocity * (angularDrag * Time.fixedDeltaTime), ForceMode.Impulse);
            
        }

        rb.AddForce(sails.GetTotalWindPush(),ForceMode.Force);
        

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