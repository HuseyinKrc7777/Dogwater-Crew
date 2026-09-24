using UnityEngine;

// Placeholder lightning: on Trigger() a light flashes twice and a jagged bolt appears far away for a
// moment. Local visual only - the strike timing comes from WeatherEffects (identical on every peer),
// the bolt's shape and bearing are random per peer, which is fine for a far-away flash.
public class LightningFlashEffect : WeatherEffect
{
    [Tooltip("Directional light used for the flash. Keep it disabled with shadows off; this script enables it only while flashing.")]
    [SerializeField] private Light flashLight;
    [Tooltip("Peak intensity in the light's unit (lux for a directional light). The sun is ~130 000 lux.")]
    [Min(0f)][SerializeField] private float flashIntensity = 150000f;
    [Min(0.05f)][SerializeField] private float flashSeconds = 0.35f;

    [Header("Bolt (optional)")]
    [SerializeField] private LineRenderer bolt;
    [Min(0f)][SerializeField] private float boltDistance = 1800f;
    [Min(0f)][SerializeField] private float boltHeight = 900f;
    [Min(2)][SerializeField] private int boltSegments = 14;
    [Min(0f)][SerializeField] private float boltJitter = 70f;

    private readonly System.Random random = new System.Random();   // not UnityEngine.Random: that state is shared game-wide
    private float flashTime = -1f;   // < 0 = idle
    private Vector3[] boltPoints;

    private void Awake()
    {
        if (flashLight != null) flashLight.enabled = false;
        if (bolt != null) bolt.enabled = false;
    }

    // Strikes are timed by WeatherEffects; intensity has no meaning here.
    public override void SetIntensity(float intensity) { }

    public override void Trigger()
    {
        flashTime = 0f;
        float bearing = (float)random.NextDouble() * 360f;
        Vector3 direction = Quaternion.Euler(0f, bearing, 0f) * Vector3.forward;

        if (flashLight != null)
        {
            // Light travels from the strike toward the viewer, slightly downward.
            flashLight.transform.rotation = Quaternion.LookRotation(-direction + Vector3.down * 0.6f);
            flashLight.enabled = true;
        }
        if (bolt != null) BuildBolt(direction);
    }

    private void BuildBolt(Vector3 direction)
    {
        if (boltPoints == null || boltPoints.Length != boltSegments + 1) boltPoints = new Vector3[boltSegments + 1];

        Vector3 ground = transform.position + direction * boltDistance;
        ground.y = 0f;   // sea level
        Vector3 top = ground + Vector3.up * boltHeight;
        Vector3 side = Vector3.Cross(direction, Vector3.up);

        for (int i = 0; i <= boltSegments; i++)
        {
            float t = i / (float)boltSegments;
            Vector3 point = Vector3.Lerp(top, ground, t);
            if (i > 0 && i < boltSegments)
            {
                point += side * (((float)random.NextDouble() * 2f - 1f) * boltJitter);
                point += direction * (((float)random.NextDouble() * 2f - 1f) * boltJitter * 0.5f);
            }
            boltPoints[i] = point;
        }

        bolt.useWorldSpace = true;
        bolt.positionCount = boltPoints.Length;
        bolt.SetPositions(boltPoints);
        bolt.enabled = true;
    }

    private void Update()
    {
        if (flashTime < 0f) return;
        flashTime += Time.deltaTime;
        float t = flashTime / flashSeconds;

        if (t >= 1f)
        {
            flashTime = -1f;
            if (flashLight != null) flashLight.enabled = false;
            if (bolt != null) bolt.enabled = false;
            return;
        }

        // Two quick pulses, the second weaker - reads as a real strike rather than a camera flash.
        float pulse = t < 0.35f ? 1f - t / 0.35f : 0.6f * Mathf.Clamp01(1f - (t - 0.45f) / 0.55f) * (t > 0.45f ? 1f : 0f);
        if (flashLight != null) flashLight.intensity = flashIntensity * pulse;
        if (bolt != null) bolt.enabled = pulse > 0.05f;
    }

    private void OnDisable()
    {
        flashTime = -1f;
        if (flashLight != null) flashLight.enabled = false;
        if (bolt != null) bolt.enabled = false;
    }
}
