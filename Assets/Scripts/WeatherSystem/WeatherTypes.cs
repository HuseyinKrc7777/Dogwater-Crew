using System;
using Unity.Netcode;

// The only weather state that goes over the network: which preset the active region shows and
// how far the fade into it has got. Presets travel as indices into WeatherDatabase, never as
// references, so this struct stays unmanaged.
// Every field must be in both NetworkSerialize AND Equals.
// default(WeatherSnapshot) means "Clear" (index 0), not "nothing" - always start from Invalid.
public struct WeatherSnapshot : INetworkSerializable, IEquatable<WeatherSnapshot>
{
    public int RegionId;
    public int FromPresetIndex;             // -1 = nothing was displayed before
    public int ToPresetIndex;               // -1 = no valid weather yet
    public double TransitionStartServerTime;
    public float TransitionSeconds;
    public uint Revision;                   // 0 only for Invalid; increments per publish

    public static WeatherSnapshot Invalid => new WeatherSnapshot
    {
        RegionId = -1,
        FromPresetIndex = -1,
        ToPresetIndex = -1,
        TransitionStartServerTime = 0d,
        TransitionSeconds = 0f,
        Revision = 0u
    };

    public bool IsValid => ToPresetIndex >= 0;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref RegionId);
        serializer.SerializeValue(ref FromPresetIndex);
        serializer.SerializeValue(ref ToPresetIndex);
        serializer.SerializeValue(ref TransitionStartServerTime);
        serializer.SerializeValue(ref TransitionSeconds);
        serializer.SerializeValue(ref Revision);
    }

    public bool Equals(WeatherSnapshot other)
    {
        return RegionId == other.RegionId
            && FromPresetIndex == other.FromPresetIndex
            && ToPresetIndex == other.ToPresetIndex
            && TransitionStartServerTime.Equals(other.TransitionStartServerTime)
            && TransitionSeconds.Equals(other.TransitionSeconds)
            && Revision == other.Revision;
    }

    public override bool Equals(object obj)
    {
        return obj is WeatherSnapshot other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(RegionId, ToPresetIndex, Revision);
    }
}

// Server-only state of one region. Never sent over the network.
// Stored in a plain array and mutated in place (regions[i].PresetIndex = x): a List<struct>
// indexer returns a copy and the write would be silently lost.
public struct WeatherRegionState
{
    // Climate, fixed at initialization
    public float Moisture;
    public float Storminess;
    public float PrevailingBearing;      // degrees, toward
    public float BeltStrengthFactor;
    public float CurrentBearing;         // degrees, toward (current belt)
    public float CurrentBaseSpeed;       // world units/s before the preset multiplier

    // Natural chain
    public int PresetIndex;
    public double NextChangeGameHour;
    public float WindStrength;           // rolled once per stay, belt factor already applied
    public float WindVeerDegrees;        // rolled once per stay

    // Force layer
    public int ForcedPresetIndex;        // -1 = none
    public double ForcedEndGameHour;     // <= 0 = indefinite
    public float ForcedWindStrength;
    public float ForcedWindVeerDegrees;

    public bool IsForced => ForcedPresetIndex >= 0;
    public int EffectivePresetIndex => IsForced ? ForcedPresetIndex : PresetIndex;
    public float EffectiveWindStrength => IsForced ? ForcedWindStrength : WindStrength;
    public float EffectiveBearing => PrevailingBearing + (IsForced ? ForcedWindVeerDegrees : WindVeerDegrees);
}
