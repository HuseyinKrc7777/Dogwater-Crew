using Unity.Netcode;
using UnityEngine;

// Local weather effects (rain, lightning). Reads the synced WeatherSnapshot every frame and blends the
// From/To presets' effect values with the same server-time fade as the sky Volumes, then hands plain
// numbers to the WeatherEffect components listed below. It knows nothing about how an effect looks.
//
// Decides nothing and sends nothing: every peer computes the same values from the same snapshot and
// server time. Lightning strikes are derived from server-time seconds with a deterministic hash, so
// all players see a strike at the same moment without any RPC (and late joiners are in step at once).
public class WeatherEffects : MonoBehaviour
{
    [Tooltip("Driven by the presets' Precipitation (0..1).")]
    [SerializeField] private WeatherEffect[] precipitationEffects;
    [Tooltip("Triggered on each lightning strike (presets' Lightning Per Minute).")]
    [SerializeField] private WeatherEffect[] lightningEffects;
    [Tooltip("Driven by the presets' Sunlight (1 = sun as authored, lower = behind clouds).")]
    [SerializeField] private WeatherEffect[] sunlightEffects;
    [Tooltip("Keeps this object - and every effect under it - on the local camera.")]
    [SerializeField] private bool followCamera = true;
    [Tooltip("Max change per second of each applied value (precipitation, sunlight). Smooths the jump when a fade is retargeted mid-way.")]
    [Min(0.01f)][SerializeField] private float precipitationChangePerSecond = 0.5f;

    // Value an effect channel has when there is no weather: dry, full sun.
    private const float NeutralPrecipitation = 0f;
    private const float NeutralSunlight = 1f;

    private WeatherManager manager;
    private float appliedPrecipitation = -1f;   // -1 = nothing applied yet
    private float appliedSunlight = -1f;
    private long lastLightningSecond = long.MinValue;

    private void Update()
    {
        // Unity null: also catches a manager destroyed with its session.
        if (manager == null)
        {
            WeatherManager candidate = WeatherManager.Instance;
            if (candidate == null || !candidate.IsSpawned) return;   // silent retry, spawn order is not guaranteed
            manager = candidate;
        }

        // Polling the snapshot (a small struct) instead of subscribing: the values depend on time anyway,
        // and a late joiner needs no special path.
        WeatherSnapshot snapshot = manager.CurrentWeather;
        if (!snapshot.IsValid || manager.Database == null)
        {
            ApplyChannel(precipitationEffects, ref appliedPrecipitation, NeutralPrecipitation, true);
            ApplyChannel(sunlightEffects, ref appliedSunlight, NeutralSunlight, true);
            return;
        }

        WeatherPreset to = manager.Database.GetPreset(snapshot.ToPresetIndex);
        WeatherPreset from = snapshot.FromPresetIndex >= 0 ? manager.Database.GetPreset(snapshot.FromPresetIndex) : to;
        float t = snapshot.GetBlend();

        float precipitation = Mathf.Lerp(from != null ? from.Precipitation : NeutralPrecipitation, to != null ? to.Precipitation : NeutralPrecipitation, t);
        ApplyChannel(precipitationEffects, ref appliedPrecipitation, precipitation, appliedPrecipitation < 0f);

        float sunlight = Mathf.Lerp(from != null ? from.Sunlight : NeutralSunlight, to != null ? to.Sunlight : NeutralSunlight, t);
        ApplyChannel(sunlightEffects, ref appliedSunlight, sunlight, appliedSunlight < 0f);

        float lightningPerMinute = Mathf.Lerp(from != null ? from.LightningPerMinute : 0f, to != null ? to.LightningPerMinute : 0f, t);
        UpdateLightning(lightningPerMinute, snapshot.RegionId);
    }

    private void LateUpdate()
    {
        if (!followCamera) return;
        Camera mainCamera = Camera.main;
        if (mainCamera != null) transform.position = mainCamera.transform.position;
    }

    private void OnDisable()
    {
        manager = null;
        // Hand every effect back its no-weather value (dry, full sun), then forget what was applied.
        ApplyChannel(precipitationEffects, ref appliedPrecipitation, NeutralPrecipitation, true);
        ApplyChannel(sunlightEffects, ref appliedSunlight, NeutralSunlight, true);
        appliedPrecipitation = -1f;
        appliedSunlight = -1f;
        lastLightningSecond = long.MinValue;
    }

    // applied < 0 means nothing was applied yet. snap = jump straight to the target.
    private void ApplyChannel(WeatherEffect[] effects, ref float applied, float target, bool snap)
    {
        float next = snap || applied < 0f ? target : Mathf.MoveTowards(applied, target, precipitationChangePerSecond * Time.deltaTime);
        if (applied >= 0f)
        {
            if (next == applied) return;
            // Skip tiny steps, but always land exactly on the target (an effect must reach a real 0 to stop).
            if (next != target && Mathf.Abs(next - applied) < 0.001f) return;
        }
        applied = next;

        if (effects == null) return;
        for (int i = 0; i < effects.Length; i++)
        {
            if (effects[i] != null) effects[i].SetIntensity(next);
        }
    }

    // One roll per whole server-time second. The same (second, region) gives the same roll on every
    // peer, so strikes line up across the crew. At most one strike per second (60 per minute).
    private void UpdateLightning(float strikesPerMinute, int regionId)
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || !networkManager.IsListening) return;

        long second = (long)System.Math.Floor(networkManager.ServerTime.Time);
        if (second == lastLightningSecond) return;
        bool firstCheck = lastLightningSecond == long.MinValue;
        lastLightningSecond = second;
        if (firstCheck || strikesPerMinute <= 0f || lightningEffects == null) return;   // never strike on the first frame

        if (Hash01(second, regionId) >= strikesPerMinute / 60f) return;
        for (int i = 0; i < lightningEffects.Length; i++)
        {
            if (lightningEffects[i] != null) lightningEffects[i].Trigger();
        }
    }

    // Deterministic 0..1 from two integers (integer mixing; identical on every machine).
    private static float Hash01(long a, int b)
    {
        unchecked
        {
            ulong x = (ulong)a * 0x9E3779B97F4A7C15UL ^ (ulong)(uint)b * 0xC2B2AE3D27D4EB4FUL;
            x ^= x >> 33;
            x *= 0xFF51AFD7ED558CCDUL;
            x ^= x >> 33;
            x *= 0xC4CEB9FE1A85EC53UL;
            x ^= x >> 33;
            return (x >> 40) / (float)(1UL << 24);
        }
    }
}
