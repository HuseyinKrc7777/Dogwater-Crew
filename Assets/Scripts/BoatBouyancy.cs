using Unity.Netcode;
using UnityEngine;

public class BoatBuoyancy : NetworkBehaviour
{
    [Header("Waves")]
    [SerializeField] Vector4 steepness;
    [SerializeField] Vector4 wavelength;
    [SerializeField] Vector4 speed;
    [SerializeField] Vector4 directions;


    [Header("Buoyancy")]
    public float strength = 1f;
    public float objectDepth = 1f;

    [Header("Effectors")]
    public Transform[] effectors;

    [Header("Smoothing")]
    public float positionLerp = 3f;
    public float rotationLerp = 3f;

    private Vector3[] effectorTargets;
    private Vector3 velocity;

    private WaterController waterController;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsOwner)
            return;

        effectorTargets = new Vector3[effectors.Length];

        waterController = GameObject
            .FindGameObjectWithTag("WaterController")
            .GetComponent<WaterController>();
    }

    void FixedUpdate()
    {
        if (!IsOwner)
            return;

        // Get synced wave params
        steepness = waterController.steepness.Value;
        wavelength = waterController.wavelength.Value;
        speed = waterController.speed.Value;
        directions = waterController.directions.Value;

        Vector3 center = Vector3.zero;
        float totalWeight = 0f;

        int count = effectors.Length;

        // --- SAMPLE WAVES ---
        for (int i = 0; i < count; i++)
        {
            Vector3 p = effectors[i].position;

            float waveY = GerstnerWaveDisplacement.GetWaveDisplacement(
                p,
                new float[] { steepness.x, steepness.y, steepness.z, steepness.w },
                new float[] { wavelength.x, wavelength.y, wavelength.z, wavelength.w },
                new float[] { speed.x, speed.y, speed.z, speed.w },
                new float[] { directions.x, directions.y, directions.z, directions.w }
            ).y;

            effectorTargets[i] = new Vector3(p.x, waveY, p.z);

            // Weight lower points more (prevents floating above crests)
            float weight = 1f;
            if (waveY < transform.position.y)
                weight = 2f;

            center += effectorTargets[i] * weight;
            totalWeight += weight;
        }

        center /= totalWeight;

        // --- NORMAL ---
        Vector3 normal = Vector3.up;

        if (count >= 3)
        {
            Vector3 a = effectorTargets[0];
            Vector3 b = effectorTargets[1];
            Vector3 c = effectorTargets[2];

            normal = Vector3.Cross(b - a, c - a).normalized;
        }

        // --- POSITION (WITH SUBMERSION) ---
        float buoyancyOffset = objectDepth;

        Vector3 targetPos = new Vector3(
            transform.position.x,
            center.y - buoyancyOffset,
            transform.position.z
        );
        if (float.IsNaN(velocity.y) || float.IsInfinity(velocity.y))
            velocity = Vector3.zero;
        transform.position = Vector3.SmoothDamp(
            transform.position,
            targetPos,
            ref velocity,
            1f / positionLerp
        );

        // --- ROTATION ---
        Quaternion targetRot = Quaternion.FromToRotation(transform.up, normal) * transform.rotation;

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRot,
            rotationLerp * Time.smoothDeltaTime
        );
    }


}
