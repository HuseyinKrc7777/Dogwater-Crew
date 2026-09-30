using System.Collections.Generic;
using UnityEngine;

// Server-side regional weather: one Markov chain per region, all of them advancing all the time.
// Plain C# on purpose - no Unity lifecycle, no networking - so it can be exercised from the Editor
// without Play Mode. WeatherManager owns one instance on the server and feeds it the game clock.
// Deterministic for a given seed and sequence of Advance() times.
public sealed class WeatherSimulation
{
    public const int MaxRegions = 4096;

    // Upper bound of catch-up steps per region per Advance(). Normal ticks need 0-1 steps; this only
    // limits the work after a huge clock jump (~100 steps for 500 game hours with the default
    // presets). MinDurationGameHours >= 0.01 guarantees termination anyway.
    private const int MaxCatchUpSteps = 1024;

    private readonly WeatherDatabase database;
    private readonly System.Random random;

    private WeatherRegionState[] regions;
    private WeatherPreset[] presets;          // snapshot of the database array at initialization
    private int[][] transitionTargets;        // per preset: resolved target indices
    private float[][] transitionWeights;      // per preset: base weights, parallel to transitionTargets
    private int rows;
    private int cols;
    private bool reportedCatchUpLimit;

    public WeatherSimulation(WeatherDatabase database, int seed)
    {
        this.database = database;
        random = new System.Random(seed);
    }

    public int RegionCount => regions != null ? regions.Length : 0;
    public bool IsInitialized { get; private set; }

    // Validates the database and rolls a starting state per region.
    // On failure returns false with a single actionable message and leaves the simulation unusable.
    public bool TryInitialize(double nowGameHours, out string error)
    {
        IsInitialized = false;
        error = null;

        if (database == null)
        {
            error = "[Weather] No WeatherDatabase assigned.";
            return false;
        }

        int presetCount = database.PresetCount;
        presets = new WeatherPreset[presetCount];
        bool anyPreset = false;
        for (int i = 0; i < presetCount; i++)
        {
            presets[i] = database.GetPreset(i);
            if (presets[i] != null) anyPreset = true;
        }
        if (!anyPreset)
        {
            error = $"[Weather] WeatherDatabase '{database.name}' has no presets.";
            return false;
        }

        rows = database.LatitudeRows;
        cols = database.LongitudeColumns;
        if (rows * cols > MaxRegions)
        {
            error = $"[Weather] WeatherDatabase '{database.name}' has {rows * cols} regions; the limit is {MaxRegions}.";
            return false;
        }

        if (!TryResolveTransitions(out error)) return false;

        ReportAuthoringWarnings();

        regions = new WeatherRegionState[rows * cols];
        for (int id = 0; id < regions.Length; id++)
        {
            ref WeatherRegionState r = ref regions[id];
            InitializeClimate(ref r, id);

            int first = PickInitial(r);
            if (first < 0)
            {
                error = $"[Weather] Every preset's initial weight is zero for the row at latitude {GetRowCentreLatitude(id):0.#}° " +
                        $"(check initialWeight and the climate sensitivities in '{database.name}').";
                regions = null;
                return false;
            }

            r.PresetIndex = first;
            r.WindStrength = RollStrength(first) * r.BeltStrengthFactor;
            r.WindVeerDegrees = RollVeer(first);
            r.NextChangeGameHour = nowGameHours + RollDuration(first);
            r.ForcedPresetIndex = -1;
        }

        reportedCatchUpLimit = false;
        IsInitialized = true;
        return true;
    }

    // Catch-up loop over every region. Weather is sequence-based: every interval that has passed is
    // a Markov step that must actually be rolled, so missed intervals are replayed, not skipped.
    public void Advance(double nowGameHours)
    {
        if (!IsInitialized || double.IsNaN(nowGameHours) || double.IsInfinity(nowGameHours)) return;

        for (int id = 0; id < regions.Length; id++)
        {
            ref WeatherRegionState r = ref regions[id];

            int steps = 0;
            while (nowGameHours >= r.NextChangeGameHour)
            {
                if (++steps > MaxCatchUpSteps)
                {
                    if (!reportedCatchUpLimit)
                    {
                        Debug.LogError($"[Weather] Region {id} needed more than {MaxCatchUpSteps} catch-up steps; skipping ahead. Check preset durations.");
                        reportedCatchUpLimit = true;
                    }
                    r.NextChangeGameHour = nowGameHours + 1d;
                    break;
                }

                int next = PickNext(r);
                if (next < 0)
                {
                    // No usable outgoing weight in this climate: stay, try again in an hour.
                    r.NextChangeGameHour = nowGameHours + 1d;
                    break;
                }

                // A self-transition only extends the stay; wind is kept so the sky and the wind agree.
                if (next != r.PresetIndex)
                {
                    r.PresetIndex = next;
                    r.WindStrength = RollStrength(next) * r.BeltStrengthFactor;
                    r.WindVeerDegrees = RollVeer(next);
                }
                r.NextChangeGameHour += RollDuration(next);
            }

            if (r.IsForced && r.ForcedEndGameHour > 0d && nowGameHours >= r.ForcedEndGameHour)
            {
                r.ForcedPresetIndex = -1;
            }
        }
    }

    // -1 on non-finite input or before initialization. Longitude wraps, so any finite value works.
    public int GetRegionId(float latitudeDegrees, float longitudeDegrees)
    {
        if (rows <= 0 || cols <= 0) return -1;
        if (float.IsNaN(latitudeDegrees) || float.IsInfinity(latitudeDegrees)) return -1;
        if (float.IsNaN(longitudeDegrees) || float.IsInfinity(longitudeDegrees)) return -1;

        float lat01 = (Mathf.Clamp(latitudeDegrees, -90f, 90f) + 90f) / 180f;
        int row = Mathf.Min(Mathf.FloorToInt(lat01 * rows), rows - 1);

        float wrapped = longitudeDegrees - 360f * Mathf.Floor((longitudeDegrees + 180f) / 360f);   // [-180, 180)
        int col = Mathf.Min(Mathf.FloorToInt((wrapped + 180f) / 360f * cols), cols - 1);

        return row * cols + col;
    }

    public float GetRowCentreLatitude(int regionId)
    {
        if (rows <= 0 || cols <= 0) return 0f;
        int row = Mathf.Clamp(regionId / cols, 0, rows - 1);
        return -90f + (row + 0.5f) * 180f / rows;
    }

    // A copy. Out of range (or not initialized) returns a state with no preset.
    public WeatherRegionState GetRegion(int regionId)
    {
        if (!IsValidRegion(regionId))
        {
            return new WeatherRegionState { PresetIndex = -1, ForcedPresetIndex = -1 };
        }
        return regions[regionId];
    }

    // Effective bearing + strength as a world vector (0° = +Z north, 90° = +X east).
    public Vector3 GetWindVector(int regionId)
    {
        if (!IsValidRegion(regionId)) return Vector3.zero;
        WeatherRegionState r = regions[regionId];
        return BearingToVector(r.EffectiveBearing, r.EffectiveWindStrength);
    }

    // Current-belt bearing x base speed x the effective preset's multiplier, so forcing a storm
    // strengthens the current too.
    public Vector3 GetCurrentVector(int regionId)
    {
        if (!IsValidRegion(regionId)) return Vector3.zero;
        WeatherRegionState r = regions[regionId];
        WeatherPreset preset = GetPresetSafe(r.EffectivePresetIndex);
        float multiplier = preset != null ? preset.CurrentMultiplier : 1f;
        return BearingToVector(r.CurrentBearing, r.CurrentBaseSpeed * multiplier);
    }

    // Overrides the region's weather; the natural chain keeps advancing underneath.
    // durationGameHours <= 0 means until cleared.
    public bool Force(int regionId, int presetIndex, double nowGameHours, float durationGameHours)
    {
        if (!IsValidRegion(regionId) || GetPresetSafe(presetIndex) == null) return false;

        ref WeatherRegionState r = ref regions[regionId];
        r.ForcedPresetIndex = presetIndex;
        r.ForcedEndGameHour = durationGameHours > 0f ? nowGameHours + durationGameHours : 0d;
        r.ForcedWindStrength = RollStrength(presetIndex) * r.BeltStrengthFactor;
        r.ForcedWindVeerDegrees = RollVeer(presetIndex);
        return true;
    }

    // Reveals wherever the natural chain has got to. Clearing an unforced region is a harmless no-op.
    public bool ClearForce(int regionId)
    {
        if (!IsValidRegion(regionId)) return false;
        regions[regionId].ForcedPresetIndex = -1;
        return true;
    }

    // Debug: rolls the region's next natural step now.
    public bool AdvanceNatural(int regionId, double nowGameHours)
    {
        if (!IsValidRegion(regionId)) return false;

        ref WeatherRegionState r = ref regions[regionId];
        int next = PickNext(r);
        if (next < 0) return false;

        if (next != r.PresetIndex)
        {
            r.PresetIndex = next;
            r.WindStrength = RollStrength(next) * r.BeltStrengthFactor;
            r.WindVeerDegrees = RollVeer(next);
        }
        r.NextChangeGameHour = nowGameHours + RollDuration(next);
        return true;
    }

    private bool IsValidRegion(int regionId)
    {
        return IsInitialized && regionId >= 0 && regionId < regions.Length;
    }

    private WeatherPreset GetPresetSafe(int index)
    {
        if (presets == null || index < 0 || index >= presets.Length) return null;
        return presets[index];
    }

    private bool TryResolveTransitions(out string error)
    {
        error = null;
        transitionTargets = new int[presets.Length][];
        transitionWeights = new float[presets.Length][];

        for (int i = 0; i < presets.Length; i++)
        {
            var targets = new List<int>();
            var weights = new List<float>();

            WeatherPreset preset = presets[i];
            if (preset != null && preset.Transitions != null)
            {
                foreach (WeatherPreset.Transition t in preset.Transitions)
                {
                    if (t == null || t.target == null) continue;   // empty Inspector row: ignored

                    int target = database.IndexOf(t.target);
                    if (target < 0)
                    {
                        error = $"[Weather] Preset '{preset.name}' has a transition to '{t.target.name}', which is not in WeatherDatabase '{database.name}'.";
                        return false;
                    }

                    targets.Add(target);
                    weights.Add(Mathf.Max(0f, t.weight));
                }
            }

            transitionTargets[i] = targets.ToArray();
            transitionWeights[i] = weights.ToArray();
        }

        return true;
    }

    private void ReportAuthoringWarnings()
    {
        for (int i = 0; i < presets.Length; i++)
        {
            WeatherPreset preset = presets[i];
            if (preset == null) continue;

            for (int j = 0; j < i; j++)
            {
                if (presets[j] == preset)
                {
                    Debug.LogWarning($"[Weather] Preset '{preset.name}' appears twice in WeatherDatabase '{database.name}' (indices {j} and {i}).", database);
                    break;
                }
            }

            float outgoing = 0f;
            foreach (float w in transitionWeights[i]) outgoing += w;
            if (outgoing <= 0f)
            {
                Debug.LogWarning($"[Weather] Preset '{preset.name}' has no outgoing transition weight; a region that enters it never leaves.", preset);
            }
        }
    }

    private void InitializeClimate(ref WeatherRegionState r, int regionId)
    {
        float lat = GetRowCentreLatitude(regionId);
        float absLat = Mathf.Abs(lat);
        bool north = lat >= 0f;

        r.Moisture = database.EvaluateMoisture(absLat);
        r.Storminess = database.EvaluateStorminess(absLat);

        WeatherDatabase.WindBelt wind = database.GetWindBelt(absLat);
        r.PrevailingBearing = north ? wind.towardBearingNorth : wind.towardBearingSouth;
        r.BeltStrengthFactor = Mathf.Max(0f, wind.strengthFactor);

        WeatherDatabase.CurrentBelt current = database.GetCurrentBelt(absLat);
        r.CurrentBearing = north ? current.towardBearingNorth : current.towardBearingSouth;
        r.CurrentBaseSpeed = Mathf.Max(0f, current.speed);
    }

    // The region's climate scales every candidate's base weight, for initial and outgoing picks alike.
    private static float ClimateWeight(WeatherPreset preset, float baseWeight, in WeatherRegionState r)
    {
        float factor = 1f
            + preset.MoistureSensitivity * (r.Moisture - 0.5f) * 2f
            + preset.StorminessSensitivity * (r.Storminess - 0.5f) * 2f;
        return Mathf.Max(0f, baseWeight * Mathf.Max(0f, factor));
    }

    private int PickInitial(in WeatherRegionState r)
    {
        float total = 0f;
        for (int i = 0; i < presets.Length; i++)
        {
            if (presets[i] != null) total += ClimateWeight(presets[i], presets[i].InitialWeight, r);
        }
        if (total <= 0f) return -1;

        double roll = random.NextDouble() * total;
        int last = -1;
        for (int i = 0; i < presets.Length; i++)
        {
            if (presets[i] == null) continue;
            float w = ClimateWeight(presets[i], presets[i].InitialWeight, r);
            if (w <= 0f) continue;
            last = i;
            roll -= w;
            if (roll < 0d) return i;
        }
        return last;   // float rounding at the very end of the range
    }

    private int PickNext(in WeatherRegionState r)
    {
        int from = r.PresetIndex;
        if (from < 0 || from >= transitionTargets.Length) return -1;

        int[] targets = transitionTargets[from];
        float[] weights = transitionWeights[from];

        float total = 0f;
        for (int k = 0; k < targets.Length; k++)
        {
            WeatherPreset target = presets[targets[k]];
            if (target != null) total += ClimateWeight(target, weights[k], r);
        }
        if (total <= 0f) return -1;

        double roll = random.NextDouble() * total;
        int last = -1;
        for (int k = 0; k < targets.Length; k++)
        {
            WeatherPreset target = presets[targets[k]];
            if (target == null) continue;
            float w = ClimateWeight(target, weights[k], r);
            if (w <= 0f) continue;
            last = targets[k];
            roll -= w;
            if (roll < 0d) return targets[k];
        }
        return last;
    }

    private float RollStrength(int presetIndex)
    {
        WeatherPreset p = presets[presetIndex];
        return Mathf.Lerp(p.MinWindStrength, p.MaxWindStrength, (float)random.NextDouble());
    }

    private float RollVeer(int presetIndex)
    {
        WeatherPreset p = presets[presetIndex];
        return ((float)random.NextDouble() * 2f - 1f) * p.MaxWindVeerDegrees;
    }

    private float RollDuration(int presetIndex)
    {
        WeatherPreset p = presets[presetIndex];
        return Mathf.Lerp(p.MinDurationGameHours, p.MaxDurationGameHours, (float)random.NextDouble());
    }

    private static Vector3 BearingToVector(float bearingDegrees, float magnitude)
    {
        float rad = bearingDegrees * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)) * magnitude;
    }
}
