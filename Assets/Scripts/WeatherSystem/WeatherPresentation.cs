using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;

// Local weather visuals: crossfades two global HDRP Volumes from the synced WeatherSnapshot.
// Runs identically on every peer and decides nothing - progress comes from server time, so a late
// joiner picks up a fade in progress instead of restarting it.
//
// Source Volume (lower priority, weight 1) shows what was on screen; target Volume (higher priority)
// fades 0 -> 1 over it. Both must sit above the sky Volume (priority 0). This class only ever assigns
// sharedProfile and weight on its own two Volumes: it never writes into a profile, and never reads
// Volume.profile (that getter clones the profile and the Volume then ignores later sharedProfile
// assignments). No NetworkObject needed - plain MonoBehaviour on a plain GameObject.
public class WeatherPresentation : MonoBehaviour
{
    [Tooltip("Global Volume, priority 10, weight 0, no profile. Shows the weather currently on screen.")]
    [SerializeField] private Volume sourceVolume;
    [Tooltip("Global Volume, priority 20, weight 0, no profile. Fades in the next weather.")]
    [SerializeField] private Volume targetVolume;

    private WeatherManager manager;
    private WeatherSnapshot snapshot = WeatherSnapshot.Invalid;
    private uint appliedRevision;
    private int displayedIndex = -1;
    private bool blending;

    private void Awake()
    {
        if (sourceVolume == null || targetVolume == null)
        {
            // Local visuals only: the simulation and the wind keep running without this component.
            Debug.LogError("[Weather] WeatherPresentation needs both Volumes assigned; weather visuals are off.", this);
            enabled = false;
            return;
        }
        if (sourceVolume.priority >= targetVolume.priority)
        {
            Debug.LogWarning("[Weather] The source Volume's priority must be lower than the target's, or fades will pop.", this);
        }

        // Start neutral: the scene looks exactly as authored until the first snapshot arrives.
        sourceVolume.weight = 0f;
        targetVolume.weight = 0f;
    }

    private void Update()
    {
        // Unity null: also catches a manager destroyed with its session.
        if (manager == null)
        {
            TryBind();
            return;
        }
        if (!blending) return;

        float t = ComputeBlend(snapshot);
        targetVolume.weight = t;
        if (t >= 1f) CommitTarget();
    }

    // Also runs before OnDestroy. The Volumes live on their own GameObjects and stay active when only
    // this component is disabled, so zero them here or the current weather would freeze on screen.
    // Re-enabling rebinds through TryBind (the late-join path), which re-applies the current weather.
    private void OnDisable()
    {
        if (manager != null) manager.ActiveWeatherChanged -= OnWeatherChanged;
        manager = null;
        snapshot = WeatherSnapshot.Invalid;
        appliedRevision = 0;
        displayedIndex = -1;
        blending = false;
        if (sourceVolume != null) sourceVolume.weight = 0f;
        if (targetVolume != null) targetVolume.weight = 0f;
    }

    // The scene starts before the manager spawns over the network, so poll until it is spawned and
    // bind once (same pattern as QuestLogUI).
    private void TryBind()
    {
        WeatherManager candidate = WeatherManager.Instance;
        if (candidate == null || !candidate.IsSpawned) return;   // silent retry

        manager = candidate;
        manager.ActiveWeatherChanged += OnWeatherChanged;
        // A late joiner already holds the value, so the change event will not fire for it: apply once now.
        OnWeatherChanged(manager.CurrentWeather);
    }

    private void OnWeatherChanged(WeatherSnapshot snap)
    {
        // New session / nothing published yet: keep the visuals, accept revision 1 again.
        if (!snap.IsValid)
        {
            appliedRevision = 0;
            return;
        }
        // The spawn apply and the bind apply can deliver the same snapshot twice.
        if (snap.Revision == appliedRevision) return;
        appliedRevision = snap.Revision;

        if (blending)
        {
            // Two Volumes cannot hold a three-way mix: collapse to the dominant side, then retarget.
            int dominant = ComputeBlend(snapshot) >= 0.5f ? snapshot.ToPresetIndex : displayedIndex;
            SetSource(dominant);
        }
        else if (displayedIndex < 0)
        {
            // First snapshot on this peer; a late joiner mid-fade starts from what the others started from.
            SetSource(snap.FromPresetIndex >= 0 ? snap.FromPresetIndex : snap.ToPresetIndex);
        }

        snapshot = snap;
        targetVolume.sharedProfile = ProfileOf(snap.ToPresetIndex);
        targetVolume.weight = 0f;
        blending = true;
        if (ComputeBlend(snap) >= 1f) CommitTarget();
    }

    private static float ComputeBlend(in WeatherSnapshot snap)
    {
        // Zero guard is load-bearing: a NaN weight breaks the HDRP Volume stack.
        if (snap.TransitionSeconds <= 0f) return 1f;
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || !networkManager.IsListening) return 1f;

        double elapsed = networkManager.ServerTime.Time - snap.TransitionStartServerTime;
        // A client's ServerTime can run slightly behind the start time, so clamp the negative side too.
        return Mathf.Clamp01((float)(elapsed / snap.TransitionSeconds));
    }

    private void CommitTarget()
    {
        SetSource(snapshot.ToPresetIndex);
        blending = false;
    }

    private void SetSource(int presetIndex)
    {
        displayedIndex = presetIndex;
        sourceVolume.sharedProfile = ProfileOf(presetIndex);
        sourceVolume.weight = 1f;
        targetVolume.weight = 0f;
    }

    // Null profile -> the VolumeManager skips that Volume, so a preset without a profile shows the sky as authored.
    private VolumeProfile ProfileOf(int presetIndex)
    {
        WeatherDatabase database = manager != null ? manager.Database : null;
        WeatherPreset preset = database != null ? database.GetPreset(presetIndex) : null;
        return preset != null ? preset.VolumeProfile : null;
    }
}
