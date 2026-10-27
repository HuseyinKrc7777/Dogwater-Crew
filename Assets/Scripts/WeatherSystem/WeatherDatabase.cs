using System;
using UnityEngine;

// The weather catalog and the world's climate. Every peer owns an identical copy (same build), so
// presets travel over the network as indices into this array - the same contract as QuestDatabase.
// Append new presets at the end; reordering changes what every index means.
[CreateAssetMenu(fileName = "WeatherDatabase", menuName = "Dogwater/Weather/Weather Database")]
public class WeatherDatabase : ScriptableObject
{
    [Serializable]
    public struct WindBelt
    {
        [Tooltip("This belt applies to row centres with |latitude| <= this value. Belts must be sorted ascending.")]
        [Range(0f, 90f)] public float maxAbsLatitude;
        [Tooltip("Bearing the wind blows TOWARD in the northern hemisphere. 0 = +Z (north), 90 = +X (east).")]
        [Range(0f, 360f)] public float towardBearingNorth;
        [Tooltip("Bearing the wind blows TOWARD in the southern hemisphere.")]
        [Range(0f, 360f)] public float towardBearingSouth;
        [Tooltip("Multiplies the preset's wind strength.")]
        [Min(0f)] public float strengthFactor;
    }

    [Serializable]
    public struct CurrentBelt
    {
        [Tooltip("This belt applies to row centres with |latitude| <= this value. Belts must be sorted ascending.")]
        [Range(0f, 90f)] public float maxAbsLatitude;
        [Tooltip("Bearing the water flows TOWARD in the northern hemisphere. 0 = +Z (north), 90 = +X (east).")]
        [Range(0f, 360f)] public float towardBearingNorth;
        [Tooltip("Bearing the water flows TOWARD in the southern hemisphere.")]
        [Range(0f, 360f)] public float towardBearingSouth;
        [Tooltip("Baseline current speed in world units per second, before the preset's currentMultiplier.")]
        [Min(0f)] public float speed;
    }

    [Header("Preset catalog - APPEND ONLY. Array order is a network contract.")]
    [SerializeField] private WeatherPreset[] presets;

    [Header("Region grid (keep both odd while the spawn is at 0,0 - see WEATHER_SYSTEM_PLAN.md §3)")]
    [Min(1)][SerializeField] private int latitudeRows = 9;
    [Min(1)][SerializeField] private int longitudeColumns = 3;

    [Header("Climate by |latitude| of the row centre (X = 0..90, Y = 0..1)")]
    [SerializeField] private AnimationCurve moistureByAbsLatitude = new AnimationCurve(
        new Keyframe(0f, 0.85f), new Keyframe(20f, 0.30f), new Keyframe(40f, 0.55f),
        new Keyframe(60f, 0.75f), new Keyframe(80f, 0.25f), new Keyframe(90f, 0.20f));
    [SerializeField] private AnimationCurve storminessByAbsLatitude = new AnimationCurve(
        new Keyframe(0f, 0.35f), new Keyframe(20f, 0.30f), new Keyframe(40f, 0.50f),
        new Keyframe(60f, 0.85f), new Keyframe(80f, 0.35f), new Keyframe(90f, 0.30f));

    // Defaults below only apply when the asset is first created; editing them later does not
    // change an existing asset.
    [Header("Prevailing wind belts")]
    [SerializeField] private WindBelt[] windBelts =
    {
        new WindBelt { maxAbsLatitude = 10f, towardBearingNorth = 270f, towardBearingSouth = 270f, strengthFactor = 0.6f },  // doldrums
        new WindBelt { maxAbsLatitude = 30f, towardBearingNorth = 225f, towardBearingSouth = 315f, strengthFactor = 1.0f },  // trade winds
        new WindBelt { maxAbsLatitude = 60f, towardBearingNorth = 45f,  towardBearingSouth = 135f, strengthFactor = 1.15f }, // westerlies
        new WindBelt { maxAbsLatitude = 90f, towardBearingNorth = 225f, towardBearingSouth = 315f, strengthFactor = 0.9f },  // polar easterlies
    };

    [Header("Ocean current belts - surface currents roughly follow the prevailing wind")]
    [SerializeField] private CurrentBelt[] currentBelts =
    {
        new CurrentBelt { maxAbsLatitude = 10f, towardBearingNorth = 270f, towardBearingSouth = 270f, speed = 0.8f },  // equatorial current, westward
        new CurrentBelt { maxAbsLatitude = 30f, towardBearingNorth = 270f, towardBearingSouth = 270f, speed = 0.6f },  // trade-wind drift, westward
        new CurrentBelt { maxAbsLatitude = 60f, towardBearingNorth = 90f,  towardBearingSouth = 90f,  speed = 0.5f },  // westerly drift, eastward
        new CurrentBelt { maxAbsLatitude = 90f, towardBearingNorth = 270f, towardBearingSouth = 270f, speed = 0.3f },  // polar drift, westward
    };

    [Header("Tuning")]
    [Min(0f)][SerializeField] private float regionSwitchDebounceSeconds = 3f;
    [Min(0.05f)][SerializeField] private float maintenanceIntervalSeconds = 0.5f;

    public int PresetCount => presets != null ? presets.Length : 0;
    public int LatitudeRows => Mathf.Max(1, latitudeRows);
    public int LongitudeColumns => Mathf.Max(1, longitudeColumns);
    public int RegionCount => LatitudeRows * LongitudeColumns;
    public float RegionSwitchDebounceSeconds => Mathf.Max(0f, regionSwitchDebounceSeconds);
    public float MaintenanceIntervalSeconds => Mathf.Max(0.05f, maintenanceIntervalSeconds);

    public float EvaluateMoisture(float absLatitude) => Mathf.Clamp01(moistureByAbsLatitude.Evaluate(absLatitude));
    public float EvaluateStorminess(float absLatitude) => Mathf.Clamp01(storminessByAbsLatitude.Evaluate(absLatitude));

    // Bounds-safe: null instead of throwing (same contract as QuestDatabase).
    public WeatherPreset GetPreset(int index)
    {
        if (presets == null || index < 0 || index >= presets.Length) return null;
        return presets[index];
    }

    // Resolves a preset reference back to its network-safe index; -1 when it is not in this database.
    public int IndexOf(WeatherPreset preset)
    {
        if (presets == null || preset == null) return -1;
        for (int i = 0; i < presets.Length; i++)
        {
            if (presets[i] == preset) return i;
        }
        return -1;
    }

    // First belt whose maxAbsLatitude covers the value; a neutral east-blowing belt if none is authored.
    public WindBelt GetWindBelt(float absLatitude)
    {
        if (windBelts != null)
        {
            for (int i = 0; i < windBelts.Length; i++)
            {
                if (absLatitude <= windBelts[i].maxAbsLatitude) return windBelts[i];
            }
        }
        return new WindBelt { maxAbsLatitude = 90f, towardBearingNorth = 90f, towardBearingSouth = 90f, strengthFactor = 1f };
    }

    // Same lookup rule as GetWindBelt; a zero-speed belt (no current) if none is authored.
    public CurrentBelt GetCurrentBelt(float absLatitude)
    {
        if (currentBelts != null)
        {
            for (int i = 0; i < currentBelts.Length; i++)
            {
                if (absLatitude <= currentBelts[i].maxAbsLatitude) return currentBelts[i];
            }
        }
        return new CurrentBelt { maxAbsLatitude = 90f, towardBearingNorth = 0f, towardBearingSouth = 0f, speed = 0f };
    }
}
