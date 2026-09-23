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

    [Header("Wind (WaterController.wind units; 1 = reference breeze)")]
    [Min(0f)][SerializeField] private float minWindStrength = 0.8f;
    [Min(0f)][SerializeField] private float maxWindStrength = 1.2f;
    [Tooltip("Random veer around the region's prevailing bearing, +/- degrees, rolled once per stay.")]
    [Range(0f, 90f)][SerializeField] private float maxWindVeerDegrees = 15f;

    [Header("Ocean current")]
    [Tooltip("Multiplies the region's current-belt speed while this state is active. 1 = climate baseline.")]
    [Range(0f, 5f)][SerializeField] private float currentMultiplier = 1f;

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
    public float MinWindStrength => Mathf.Max(0f, minWindStrength);
    public float MaxWindStrength => Mathf.Max(MinWindStrength, maxWindStrength);
    public float MaxWindVeerDegrees => Mathf.Clamp(maxWindVeerDegrees, 0f, 90f);
    public float CurrentMultiplier => Mathf.Clamp(currentMultiplier, 0f, 5f);
    public float MoistureSensitivity => moistureSensitivity;
    public float StorminessSensitivity => storminessSensitivity;
}
