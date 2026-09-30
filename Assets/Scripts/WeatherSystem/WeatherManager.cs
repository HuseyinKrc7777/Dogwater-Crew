using System;
using Unity.Netcode;
using UnityEngine;

// Server-authoritative regional weather. The server runs WeatherSimulation (every region, all the
// time), picks the region the player ship is in, publishes that region's weather as ONE
// NetworkVariable<WeatherSnapshot>, and drives WaterController's wind and current toward it.
// Clients decide nothing: they read the snapshot (for visuals) and the existing wind/current NVs.
// Must sit on its OWN GameObject with a NetworkObject - never on a shared singleton object, since
// other singletons there Destroy(gameObject) on duplicates.
public class WeatherManager : NetworkBehaviour
{
    [SerializeField] private WeatherDatabase database;

    [Header("Debug (server / host only)")]
    [Tooltip("Preset used by the 'Force Debug Preset' context menu.")]
    [SerializeField] private WeatherPreset debugPreset;
    [Tooltip("In-game hours; <= 0 means until cleared.")]
    [SerializeField] private float debugForceDurationGameHours = 0f;
    [Tooltip("One log line per active-region weather change. Never per tick.")]
    [SerializeField] private bool logTransitions = false;

    // NetworkVariables are not drawn by the Inspector, so the server mirrors what it is doing here every
    // maintenance tick. Display only: edits are overwritten, and the fields stay empty on clients.
    [Header("Debug Readout (server / host, display only)")]
    [SerializeField] private string readoutWeather;
    [SerializeField] private int readoutRegionId = -1;
    [SerializeField] private float readoutRowLatitude;
    [SerializeField] private float readoutGameHoursToNaturalChange;
    [SerializeField] private Vector3 readoutWind;
    [SerializeField] private Vector3 readoutCurrent;

    // Not readonly: a readonly NetworkVariable broke initialization once (HOW_IT_WORKS.md, Adım 2).
    private NetworkVariable<WeatherSnapshot> currentWeather = new NetworkVariable<WeatherSnapshot>(
        WeatherSnapshot.Invalid,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public static WeatherManager Instance { get; private set; }
    public WeatherSnapshot CurrentWeather => IsSpawned ? currentWeather.Value : WeatherSnapshot.Invalid;
    public WeatherDatabase Database => database;
    public int ActiveRegionId => IsServer ? activeRegionId : -1;

    // Raised on every peer when the published weather changes. Plain C# so consumers stay NGO-free.
    public event Action<WeatherSnapshot> ActiveWeatherChanged;

    // ---- server-only state (reset on every spawn) ----
    private WeatherSimulation simulation;
    private bool initialized;
    private bool initializationFailed;
    private float maintenanceTimer;
    private int activeRegionId;
    private int candidateRegionId;
    private float candidateSince;
    private int lastPublishedRegionId;
    private int lastPublishedPresetIndex;
    private uint revision;
    private VectorTransition windTransition;
    private VectorTransition currentTransition;
    private bool warnedMissingClock;
    private bool warnedMissingShip;
    private bool warnedMissingWater;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // Deliberately NOT Destroy(gameObject): that would take whatever else lives on this object with it.
            Debug.LogWarning("[Weather] Duplicate WeatherManager; this copy stays inert.", this);
            return;
        }
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        // Required: NGO calls OnNetworkSpawn on every NetworkBehaviour of an active GameObject, even a disabled one.
        if (Instance != this) return;

        currentWeather.OnValueChanged += HandleWeatherChanged;
        if (IsServer) ResetServerState();

        // OnValueChanged is not an initial-state callback: a late joiner already holds the value.
        HandleWeatherChanged(WeatherSnapshot.Invalid, currentWeather.Value);
    }

    public override void OnNetworkDespawn()
    {
        if (Instance == this)
        {
            currentWeather.OnValueChanged -= HandleWeatherChanged;
            if (IsServer && WaterController.Instance != null)
            {
                WaterController.Instance.ClearWeatherWind();
                WaterController.Instance.ClearWeatherCurrent();
            }
        }
        base.OnNetworkDespawn();
    }

    public override void OnDestroy()
    {
        if (Instance == this) Instance = null;
        base.OnDestroy();   // NGO disposes NetworkVariable fields here. Never Dispose() manually.
    }

    // The ONLY raise path. NGO already fires OnValueChanged on the writer, so raising directly after a
    // write would fire twice on the host.
    private void HandleWeatherChanged(WeatherSnapshot previous, WeatherSnapshot current)
    {
        ActiveWeatherChanged?.Invoke(current);
    }

    private void ResetServerState()
    {
        simulation = null;
        initialized = false;
        initializationFailed = false;
        maintenanceTimer = 0f;
        activeRegionId = -1;
        candidateRegionId = -1;
        candidateSince = 0f;
        lastPublishedRegionId = -1;
        lastPublishedPresetIndex = -1;
        revision = 0u;
        windTransition = default;
        currentTransition = default;
        warnedMissingClock = false;
        warnedMissingShip = false;
        warnedMissingWater = false;
        readoutWeather = string.Empty;
        readoutRegionId = -1;
        readoutRowLatitude = 0f;
        readoutGameHoursToNaturalChange = 0f;
        readoutWind = Vector3.zero;
        readoutCurrent = Vector3.zero;
        currentWeather.Value = WeatherSnapshot.Invalid;
    }

    private void Update()
    {
        if (!IsServer || !IsSpawned || Instance != this) return;

        if (!initialized)
        {
            TryInitializeServer();
            return;
        }

        UpdateTransitions();

        maintenanceTimer += Time.unscaledDeltaTime;
        if (maintenanceTimer < database.MaintenanceIntervalSeconds) return;
        maintenanceTimer = 0f;

        GameDayClock clock = GameDayClock.Instance;
        if (clock == null) return;   // Unity null: also catches a destroyed clock during teardown

        double now = TotalGameHours(clock);
        simulation.Advance(now);
        UpdateActiveRegion();
        PublishIfChanged();
        UpdateReadout(now);
    }

    private void UpdateReadout(double nowGameHours)
    {
        readoutRegionId = activeRegionId;
        if (activeRegionId >= 0)
        {
            WeatherRegionState region = simulation.GetRegion(activeRegionId);
            WeatherPreset preset = database.GetPreset(region.EffectivePresetIndex);
            readoutWeather = (preset != null ? preset.DisplayName : "?") + (region.IsForced ? " [forced]" : "");
            readoutRowLatitude = simulation.GetRowCentreLatitude(activeRegionId);
            readoutGameHoursToNaturalChange = (float)(region.NextChangeGameHour - nowGameHours);
        }

        WaterController water = WaterController.Instance;
        if (water != null)
        {
            readoutWind = water.wind.Value;
            readoutCurrent = water.current.Value;
        }
    }

    // ---------------- initialization ----------------

    private void TryInitializeServer()
    {
        if (initializationFailed) return;

        GameDayClock clock = GameDayClock.Instance;
        if (clock == null || !clock.IsSpawned)
        {
            if (!warnedMissingClock)
            {
                Debug.LogWarning("[Weather] Waiting for GameDayClock to spawn before starting the weather.", this);
                warnedMissingClock = true;
            }
            return;
        }

        int seed = Environment.TickCount;
        simulation = new WeatherSimulation(database, seed);
        if (!simulation.TryInitialize(TotalGameHours(clock), out string error))
        {
            // Fatal configuration: no simulation, no wind override - editorWind keeps working.
            Debug.LogError(error, this);
            initializationFailed = true;
            simulation = null;
            return;
        }

        WarnAboutShortStays(clock);
        initialized = true;
        if (logTransitions) Debug.Log($"[Weather] Started: {simulation.RegionCount} regions, seed {seed}.", this);
    }

    // A fade longer than the shortest stay means the next state can start before the fade finishes.
    private void WarnAboutShortStays(GameDayClock clock)
    {
        float realSecondsPerGameHour = clock.dayLengthSeconds / 24f;
        for (int i = 0; i < database.PresetCount; i++)
        {
            WeatherPreset preset = database.GetPreset(i);
            if (preset == null) continue;
            float shortestStay = preset.MinDurationGameHours * realSecondsPerGameHour;
            if (preset.TransitionSeconds > shortestStay)
            {
                Debug.LogWarning($"[Weather] Preset '{preset.name}' fades for {preset.TransitionSeconds:0.#} s but can last only {shortestStay:0.#} s.", preset);
            }
        }
    }

    // Server-only: dayTimer is a server-local field.
    private static double TotalGameHours(GameDayClock clock)
    {
        return ((clock.CurrentDay - 1) + clock.dayTimer / clock.dayLengthSeconds) * 24.0;
    }

    // ---------------- active region ----------------

    private void UpdateActiveRegion()
    {
        Ship ship = Ship.PlayerShip;
        if (ship == null)   // Unity null: Ship never clears this static
        {
            if (!warnedMissingShip)
            {
                Debug.LogWarning("[Weather] No player ship yet; weather will follow it once it spawns.", this);
                warnedMissingShip = true;
            }
            return;
        }

        GlobalCoordinate coordinate = ship.coordinate;
        if (coordinate == null) return;

        int sampled = simulation.GetRegionId(coordinate.latitude.GetDegree(), coordinate.longitude.GetDegree());
        if (sampled < 0) return;

        // The first region is taken immediately; later changes must hold for the debounce time so
        // sailing along a boundary does not flip the weather back and forth.
        if (activeRegionId < 0)
        {
            activeRegionId = sampled;
            candidateRegionId = -1;
        }
        else if (sampled == activeRegionId)
        {
            candidateRegionId = -1;
        }
        else if (sampled != candidateRegionId)
        {
            candidateRegionId = sampled;
            candidateSince = Time.unscaledTime;
        }
        else if (Time.unscaledTime - candidateSince >= database.RegionSwitchDebounceSeconds)
        {
            activeRegionId = sampled;
            candidateRegionId = -1;
        }
    }

    // ---------------- publishing ----------------

    private void PublishIfChanged()
    {
        if (activeRegionId < 0) return;

        WeatherRegionState region = simulation.GetRegion(activeRegionId);
        int effective = region.EffectivePresetIndex;
        if (effective < 0) return;
        if (activeRegionId == lastPublishedRegionId && effective == lastPublishedPresetIndex) return;

        WeatherPreset target = database.GetPreset(effective);
        // First publish snaps: there is nothing on screen to fade from.
        float seconds = (lastPublishedPresetIndex < 0 || target == null) ? 0f : target.TransitionSeconds;

        currentWeather.Value = new WeatherSnapshot
        {
            RegionId = activeRegionId,
            FromPresetIndex = lastPublishedPresetIndex,   // what peers are displaying NOW
            ToPresetIndex = effective,
            TransitionStartServerTime = NetworkManager.ServerTime.Time,
            TransitionSeconds = seconds,
            Revision = ++revision
        };

        if (logTransitions)
        {
            WeatherPreset from = database.GetPreset(lastPublishedPresetIndex);
            Debug.Log($"[Weather] Region {activeRegionId} (row {simulation.GetRowCentreLatitude(activeRegionId):0}°): " +
                      $"{(from != null ? from.DisplayName : "-")} -> {(target != null ? target.DisplayName : "?")} over {seconds:0.#} s" +
                      $"{(region.IsForced ? " [forced]" : "")}", this);
        }

        lastPublishedRegionId = activeRegionId;
        lastPublishedPresetIndex = effective;

        float now = Time.unscaledTime;
        windTransition.Begin(simulation.GetWindVector(activeRegionId), seconds, now);
        currentTransition.Begin(simulation.GetCurrentVector(activeRegionId), seconds, now);
    }

    private void UpdateTransitions()
    {
        bool windDue = windTransition.TryGetWrite(Time.unscaledTime, out Vector3 wind, out bool windDone);
        bool currentDue = currentTransition.TryGetWrite(Time.unscaledTime, out Vector3 current, out bool currentDone);
        if (!windDue && !currentDue) return;

        WaterController water = WaterController.Instance;
        if (water == null)   // Unity null: WaterController never clears its static
        {
            if (!warnedMissingWater)
            {
                Debug.LogWarning("[Weather] No WaterController; wind and current will apply once it exists.", this);
                warnedMissingWater = true;
            }
            return;   // retried next frame
        }

        if (windDue)
        {
            water.SetWeatherWind(wind);
            windTransition.MarkApplied(wind, windDone);
        }
        if (currentDue)
        {
            water.SetWeatherCurrent(current);
            currentTransition.MarkApplied(current, currentDone);
        }
    }

    // ---------------- server API (forces / debug) ----------------

    public int GetRegionId(float latitudeDegrees, float longitudeDegrees)
    {
        return simulation != null ? simulation.GetRegionId(latitudeDegrees, longitudeDegrees) : -1;
    }

    // durationGameHours <= 0 means until cleared. The natural chain keeps advancing underneath.
    public bool ForceWeatherAtRegion(int regionId, WeatherPreset preset, float durationGameHours = 0f)
    {
        if (!CanUseServerApi(nameof(ForceWeatherAtRegion))) return false;

        int presetIndex = database.IndexOf(preset);
        if (presetIndex < 0 || !simulation.Force(regionId, presetIndex, TotalGameHours(GameDayClock.Instance), durationGameHours))
        {
            Debug.LogWarning($"[Weather] Cannot force '{(preset != null ? preset.name : "null")}' in region {regionId}.", this);
            return false;
        }
        PublishIfChanged();
        return true;
    }

    public bool ClearForcedWeatherAtRegion(int regionId)
    {
        if (!CanUseServerApi(nameof(ClearForcedWeatherAtRegion))) return false;
        if (!simulation.ClearForce(regionId))
        {
            Debug.LogWarning($"[Weather] Cannot clear force in region {regionId}.", this);
            return false;
        }
        PublishIfChanged();
        return true;
    }

    public bool AdvanceRegionNaturalWeather(int regionId)
    {
        if (!CanUseServerApi(nameof(AdvanceRegionNaturalWeather))) return false;
        if (!simulation.AdvanceNatural(regionId, TotalGameHours(GameDayClock.Instance)))
        {
            Debug.LogWarning($"[Weather] Cannot advance region {regionId}.", this);
            return false;
        }
        PublishIfChanged();
        return true;
    }

    private bool CanUseServerApi(string caller)
    {
        if (IsServer && IsSpawned && Instance == this && initialized && GameDayClock.Instance != null) return true;
        Debug.LogWarning($"[Weather] {caller} works only on the server after the weather has started.", this);
        return false;
    }

    [ContextMenu("Weather/Force Debug Preset In Active Region")]
    private void ForceDebugPresetInActiveRegion()
    {
        ForceWeatherAtRegion(activeRegionId, debugPreset, debugForceDurationGameHours);
    }

    [ContextMenu("Weather/Clear Force In Active Region")]
    private void ClearForceInActiveRegion()
    {
        ClearForcedWeatherAtRegion(activeRegionId);
    }

    [ContextMenu("Weather/Advance Active Region")]
    private void AdvanceActiveRegion()
    {
        AdvanceRegionNaturalWeather(activeRegionId);
    }

    // Fades a horizontal vector by interpolating bearing and magnitude separately, so a veer rotates
    // the wind instead of shrinking it through zero. Used twice: wind and current.
    private struct VectorTransition
    {
        private float fromBearing;
        private float fromMagnitude;
        private float toBearing;
        private float toMagnitude;
        private float startTime;
        private float seconds;
        private bool active;
        private bool hasApplied;
        private Vector3 applied;

        public void Begin(Vector3 target, float fadeSeconds, float now)
        {
            // Retargeting mid-fade starts from what is applied now, so it never jumps.
            Vector3 from = hasApplied ? applied : target;
            Decompose(from, out fromBearing, out fromMagnitude);
            Decompose(target, out toBearing, out toMagnitude);

            // A zero vector has no direction: borrow the other end's, so only the strength ramps.
            if (fromMagnitude < 0.0001f) fromBearing = toBearing;
            if (toMagnitude < 0.0001f) toBearing = fromBearing;

            startTime = now;
            seconds = fadeSeconds;
            active = true;
        }

        // True when a new value should be written. Small steps are skipped so the NetworkVariable
        // only changes when the difference is noticeable.
        public bool TryGetWrite(float now, out Vector3 value, out bool finished)
        {
            value = applied;
            finished = false;
            if (!active) return false;

            float t = seconds <= 0.01f ? 1f : Mathf.Clamp01((now - startTime) / seconds);
            finished = t >= 1f;
            value = Compose(Mathf.LerpAngle(fromBearing, toBearing, t), Mathf.Lerp(fromMagnitude, toMagnitude, t));

            if (!finished && hasApplied
                && Vector3.Angle(value, applied) < 0.5f
                && Mathf.Abs(value.magnitude - applied.magnitude) < 0.005f)
            {
                return false;
            }
            return true;
        }

        public void MarkApplied(Vector3 value, bool finished)
        {
            applied = value;
            hasApplied = true;
            if (finished) active = false;
        }

        // 0° = +Z (north), 90° = +X (east) - the same axes Ship uses for latitude/longitude.
        private static void Decompose(Vector3 v, out float bearing, out float magnitude)
        {
            magnitude = new Vector2(v.x, v.z).magnitude;
            bearing = Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;
        }

        private static Vector3 Compose(float bearing, float magnitude)
        {
            float rad = bearing * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)) * magnitude;
        }
    }
}
