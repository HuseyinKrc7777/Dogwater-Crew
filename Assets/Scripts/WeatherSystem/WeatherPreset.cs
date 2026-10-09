using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// One weather state (Clear, Rain, Storm, ...). Adding a new weather type means adding one of these
// assets to the WeatherDatabase plus a few incoming transitions - no code change.
// Every getter clamps, so bad Inspector data degrades instead of throwing.
[CreateAssetMenu(fileName = "WeatherPreset", menuName = "Dogwater/Weather/Weather Preset")]
public class WeatherPreset : ScriptableObject
{
    [Serializable]
    public class Transition
    {
        public WeatherPreset target;
        [Min(0f)] public float weight = 1f;
    }

    [SerializeField] private string displayName = "Weather";

    [Tooltip("Weather-only HDRP Volume Profile. Must follow the authoring rules in WEATHER_SYSTEM_PLAN.md §8.3.")]
    [SerializeField] private VolumeProfile volumeProfile;

    [Header("Chain")]
    [Tooltip("Relative chance of this state when a region starts fresh.")]
    [Min(0f)][SerializeField] private float initialWeight = 1f;
    [Tooltip("Outgoing transitions. A transition to itself extends the stay.")]
    [SerializeField] private List<Transition> transitions = new List<Transition>();

    [Header("Timing")]
    [Tooltip("Visual and wind crossfade length in REAL seconds.")]
    [Min(0f)][SerializeField] private float transitionSeconds = 8f;
    [Tooltip("How long this state lasts once entered, in IN-GAME hours.")]
    [Min(0.01f)][SerializeField] private float minDurationGameHours = 3f;
    [Min(0.01f)][SerializeField] private float maxDurationGameHours = 8f;
    [Tooltip("Climate-dependent stay length: multiplies the rolled duration, sampled at the region's row-centre " +
             "|latitude| (0-90). 1 = unchanged. Clamped to 0.1-10; an empty curve counts as 1 everywhere.")]
    [SerializeField] private AnimationCurve durationScaleByAbsLatitude = AnimationCurve.Constant(0f, 90f, 1f);

    [Header("Wind (WaterController.wind units; 1 = reference breeze)")]
    [Min(0f)][SerializeField] private float minWindStrength = 0.8f;
    [Min(0f)][SerializeField] private float maxWindStrength = 1.2f;
    [Tooltip("Random veer around the region's prevailing bearing, +/- degrees, rolled once per stay.")]
    [Range(0f, 90f)][SerializeField] private float maxWindVeerDegrees = 15f;

    [Header("Ocean current")]
    [Tooltip("Multiplies the region's current-belt speed while this state is active. 1 = climate baseline.")]
    [Range(0f, 5f)][SerializeField] private float currentMultiplier = 1f;

    [Header("Effects (local visuals, driven by WeatherEffects)")]
    [Tooltip("0 = dry, 1 = the heaviest rain the effect prefabs show.")]
    [Range(0f, 1f)][SerializeField] private float precipitation;
    [Tooltip("Average lightning strikes per real minute. 0 = none.")]
    [Range(0f, 60f)][SerializeField] private float lightningPerMinute;
    [Tooltip("Sun light strength. 1 = as authored in the scene, lower = sun behind clouds.")]
    [Range(0f, 1f)][SerializeField] private float sunlight = 1f;

    [Header("Sea state (driven by WeatherSeaState; scales the sea set by the database's Sea Base Wind Speed)")]
    [Tooltip("Height of the big swell (ocean band 0). 1 = the full base sea, lower = calmer.")]
    [Range(0f, 1f)][SerializeField] private float seaSwell = 1f;
    [Tooltip("Height of the shorter, choppy waves (ocean band 1). 1 = the full base sea, lower = calmer.")]
    [Range(0f, 1f)][SerializeField] private float seaChop = 1f;

    [Header("Climate affinity")]
    [Range(-1f, 1f)][SerializeField] private float moistureSensitivity;
    [Range(-1f, 1f)][SerializeField] private float storminessSensitivity;

    public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
    public VolumeProfile VolumeProfile => volumeProfile;
    public float InitialWeight => Mathf.Max(0f, initialWeight);
    public IReadOnlyList<Transition> Transitions => transitions;
    public float TransitionSeconds => Mathf.Max(0f, transitionSeconds);
    public float MinDurationGameHours => Mathf.Max(0.01f, minDurationGameHours);
    public float MaxDurationGameHours => Mathf.Max(MinDurationGameHours, maxDurationGameHours);
    // An empty AnimationCurve evaluates to 0, which would make every stay zero-length - treat it as 1.
    public float GetDurationScale(float absLatitude)
    {
        if (durationScaleByAbsLatitude == null || durationScaleByAbsLatitude.length == 0) return 1f;
        float scale = durationScaleByAbsLatitude.Evaluate(Mathf.Clamp(absLatitude, 0f, 90f));
        if (float.IsNaN(scale) || float.IsInfinity(scale)) return 1f;
        return Mathf.Clamp(scale, 0.1f, 10f);
    }
    public float MinWindStrength => Mathf.Max(0f, minWindStrength);
    public float MaxWindStrength => Mathf.Max(MinWindStrength, maxWindStrength);
    public float MaxWindVeerDegrees => Mathf.Clamp(maxWindVeerDegrees, 0f, 90f);
    public float CurrentMultiplier => Mathf.Clamp(currentMultiplier, 0f, 5f);
    public float Precipitation => Mathf.Clamp01(precipitation);
    public float LightningPerMinute => Mathf.Clamp(lightningPerMinute, 0f, 60f);
    public float Sunlight => Mathf.Clamp01(sunlight);
    public float SeaSwell => Mathf.Clamp01(seaSwell);
    public float SeaChop => Mathf.Clamp01(seaChop);
    public float MoistureSensitivity => moistureSensitivity;
    public float StorminessSensitivity => storminessSensitivity;
}
