using UnityEngine;

// Adapter for any Particle System based effect (most rain/snow prefabs). Put it on the prefab's root:
// it scales the emission of EVERY Particle System underneath by the intensity, so an imported prefab
// works without code. At 0 it stops emitting and lets the drops already in the air finish falling.
public class ParticleWeatherEffect : WeatherEffect
{
    [Tooltip("Pushes particles sideways with the synced wind (WaterController.wind x this). 0 = leave the prefab's own motion alone.")]
    [Min(0f)][SerializeField] private float windInfluence = 0f;

    private ParticleSystem[] systems;
    private float[] baseRates;
    private float intensity;
    private Vector3 appliedWind = new Vector3(float.NaN, 0f, 0f);

    private void Awake()
    {
        systems = GetComponentsInChildren<ParticleSystem>(true);
        baseRates = new float[systems.Length];
        for (int i = 0; i < systems.Length; i++)
        {
            baseRates[i] = systems[i].emission.rateOverTimeMultiplier;
        }
        Apply(0f);
    }

    public override void SetIntensity(float value)
    {
        intensity = Mathf.Clamp01(value);
        if (systems != null) Apply(intensity);
    }

    private void Apply(float value)
    {
        for (int i = 0; i < systems.Length; i++)
        {
            ParticleSystem system = systems[i];
            if (system == null) continue;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTimeMultiplier = baseRates[i] * value;

            if (value > 0f)
            {
                if (!system.isEmitting) system.Play(false);
            }
            else if (system.isEmitting)
            {
                system.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }
        }
    }

    private void Update()
    {
        if (windInfluence <= 0f || intensity <= 0f || systems == null) return;

        WaterController water = WaterController.Instance;   // Unity null: WaterController never clears its static
        if (water == null) return;

        Vector3 wind = water.wind.Value * windInfluence;
        wind.y = 0f;
        if ((wind - appliedWind).sqrMagnitude < 0.0001f) return;
        appliedWind = wind;

        for (int i = 0; i < systems.Length; i++)
        {
            if (systems[i] == null) continue;
            ParticleSystem.VelocityOverLifetimeModule velocity = systems[i].velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            // All three axes must use the same curve mode, so y is set explicitly.
            velocity.x = new ParticleSystem.MinMaxCurve(wind.x);
            velocity.y = new ParticleSystem.MinMaxCurve(0f);
            velocity.z = new ParticleSystem.MinMaxCurve(wind.z);
        }
    }
}
