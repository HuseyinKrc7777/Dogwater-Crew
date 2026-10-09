using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

// Storm waves: drives the ocean WaterSurface (the one WaterController samples) from the synced
// WeatherSnapshot. Runs on every peer and decides nothing - every peer computes the same sea from the
// same snapshot and server time, like the sky Volumes, so no extra network traffic is needed.
//
// NOT a WeatherEffect: the sea is gameplay. BoatMovement (on the ship's owner) and PlayerFloat (on each
// player's owner) float on this surface through WaterController.GetWave, and the band multipliers below
// apply to those queries as well as to rendering.
//
// Two HDRP controls with very different costs (HDRP 17.3 WaterSurface.Simulation.cs):
// - largeWindSpeed is part of the wave spectrum: any change rebuilds it and resets the simulation time.
//   So it is written ONCE per session (the database's Sea Base Wind Speed = the full storm sea).
// - largeBand0/1Multiplier ("Amplitude Dimmer", 0..1) are plain rendering parameters: no rebuild, safe to
//   change every frame. The presets' Sea Swell/Chop dim the storm sea down for calmer weather.
// Disabling this component hands the surface back exactly as the scene authored it.
//
// Underwater rendering is also gated here because the storm sea causes the problem: HDRP treats the camera as
// "possibly underwater" while it is below surface + 2 x max wave height (HDRP 17.3
// WaterSystem.Underwater.cs, EvaluateUnderWaterSurface), and that height comes from the spectrum (the base
// wind above), not from the band multipliers. With the storm base the deck camera is always inside it, so
// HDRP stops culling the water's back faces (WaterSystem.cs, _CullWaterMask), and folded wave crests show
// their back face, which HDRP shades as seen from below: almost clear, so the crest looks transparent.
// While the camera is clearly above the local water, underwater rendering has nothing to show anyway, so it
// is switched off there and back on before the camera can reach the water.
public class WeatherSeaState : MonoBehaviour
{
    [Tooltip("Max change per second of each band multiplier. Fades are slower than this; it only smooths the " +
             "jump when a fade is retargeted mid-way (the ship would otherwise be kicked by a sudden wave change).")]
    [Min(0.001f)][SerializeField] private float maxChangePerSecond = 0.05f;

    [Header("Underwater rendering (only if the scene's WaterSurface has it enabled)")]
    [Tooltip("Underwater rendering is switched ON when the camera comes closer than this to the water below it. " +
             "Must leave enough room for a falling camera to be caught before it reaches the water.")]
    [Min(0f)][SerializeField] private float underWaterEnableBelowMeters = 2.5f;
    [Tooltip("Underwater rendering is switched OFF when the camera is higher than this above the water below it. " +
             "The gap to the enable height stops it from toggling at the boundary.")]
    [Min(0f)][SerializeField] private float underWaterDisableAboveMeters = 3.5f;

    private WeatherManager manager;
    private WaterSurface surface;

    // The surface as the scene authored it, restored on disable / when weather is gone.
    private bool captured;
    private float authoredWindSpeed;
    private float authoredBand0;
    private float authoredBand1;
    private bool authoredUnderWater;

    private bool applied;   // false = next write snaps instead of easing
    private float appliedBand0;
    private float appliedBand1;

    // Own search state: WaterController.GetWave shares one warm-start result between all of its callers.
    private WaterSearchParameters cameraSearch;
    private WaterSearchResult cameraSearchResult;
    private bool cameraSearchSeeded;
    private bool hasLastCameraY;
    private float lastCameraY;

    private void Update()
    {
        if (!TryBind()) return;

        WeatherSnapshot snapshot = manager.CurrentWeather;
        WeatherDatabase database = manager.Database;
        if (!snapshot.IsValid || database == null)
        {
            // Nothing published yet (or no database): the sea stays as authored.
            RestoreAuthored();
            return;
        }

        // Spectrum change: once, and only when it actually differs (each write rebuilds the waves).
        float baseWindSpeed = database.SeaBaseWindSpeedKmh > 0f ? database.SeaBaseWindSpeedKmh : authoredWindSpeed;
        if (!Mathf.Approximately(surface.largeWindSpeed, baseWindSpeed))
        {
            surface.largeWindSpeed = baseWindSpeed;
        }

        WeatherPreset to = database.GetPreset(snapshot.ToPresetIndex);
        WeatherPreset from = snapshot.FromPresetIndex >= 0 ? database.GetPreset(snapshot.FromPresetIndex) : to;
        float t = snapshot.GetBlend();
        float band0 = Mathf.Lerp(from != null ? from.SeaSwell : authoredBand0, to != null ? to.SeaSwell : authoredBand0, t);
        float band1 = Mathf.Lerp(from != null ? from.SeaChop : authoredBand1, to != null ? to.SeaChop : authoredBand1, t);
        ApplyBands(band0, band1);
        UpdateUnderWater();
    }

    private void OnDisable()
    {
        RestoreAuthored();
        manager = null;
        surface = null;
        captured = false;
    }

    // Poll until both the manager and the ocean exist (spawn order is not guaranteed), and re-capture
    // when the surface changes (new session / scene reload). Unity null catches destroyed objects.
    private bool TryBind()
    {
        if (manager == null)
        {
            WeatherManager candidate = WeatherManager.Instance;
            if (candidate == null || !candidate.IsSpawned) return false;   // silent retry
            manager = candidate;
        }

        WaterController water = WaterController.Instance;
        WaterSurface current = water != null ? water.targetSurface : null;
        if (current == null) return false;

        if (current != surface)
        {
            surface = current;
            authoredWindSpeed = surface.largeWindSpeed;
            authoredBand0 = surface.largeBand0Multiplier;
            authoredBand1 = surface.largeBand1Multiplier;
            authoredUnderWater = surface.underWater;
            captured = true;
            applied = false;
            cameraSearchSeeded = false;
            hasLastCameraY = false;
        }
        return true;
    }

    private void UpdateUnderWater()
    {
        if (!authoredUnderWater) return;   // the scene opted out of underwater rendering: leave it alone

        // No camera or no answer from the search = keep it on, which is exactly HDRP's own behaviour.
        bool on = surface.underWater;
        Camera cam = Camera.main;
        if (cam == null || !TryGetHeightAboveWater(cam.transform.position, out float height))
        {
            on = true;
            hasLastCameraY = false;
        }
        else
        {
            // This runs before the camera moves this frame, so the frame about to render sees it one step
            // further. Look ahead by last frame's drop: at low fps a falling camera covers metres per frame
            // (measured: 3.4 m at ~5 fps) and would otherwise render one frame underwater with this still off.
            float cameraY = cam.transform.position.y;
            float drop = hasLastCameraY ? Mathf.Min(0f, cameraY - lastCameraY) : 0f;
            lastCameraY = cameraY;
            hasLastCameraY = true;
            height += drop;

            if (height < underWaterEnableBelowMeters) on = true;
            else if (height > Mathf.Max(underWaterEnableBelowMeters, underWaterDisableAboveMeters)) on = false;
        }

        if (surface.underWater != on) surface.underWater = on;
    }

    // Same CPU search the ship and swimming use, so it sees the same waves (band multipliers included).
    private bool TryGetHeightAboveWater(Vector3 position, out float height)
    {
        if (cameraSearchSeeded) cameraSearch.startPositionWS = cameraSearchResult.candidateLocationWS;
        else cameraSearch.startPositionWS = position;
        cameraSearch.targetPositionWS = position;
        cameraSearch.error = 0.05f;      // metres matter here, not centimetres
        cameraSearch.maxIterations = 8;

        if (!surface.ProjectPointOnWaterSurface(cameraSearch, out cameraSearchResult))
        {
            cameraSearchSeeded = false;
            height = 0f;
            return false;
        }
        cameraSearchSeeded = true;
        height = position.y - cameraSearchResult.projectedPositionWS.y;
        return true;
    }

    private void ApplyBands(float band0, float band1)
    {
        if (applied)
        {
            float step = maxChangePerSecond * Time.deltaTime;
            band0 = Mathf.MoveTowards(appliedBand0, band0, step);
            band1 = Mathf.MoveTowards(appliedBand1, band1, step);
        }
        applied = true;
        appliedBand0 = band0;
        appliedBand1 = band1;
        surface.largeBand0Multiplier = band0;
        surface.largeBand1Multiplier = band1;
    }

    private void RestoreAuthored()
    {
        if (!captured || surface == null) return;
        if (surface.largeWindSpeed != authoredWindSpeed) surface.largeWindSpeed = authoredWindSpeed;
        surface.largeBand0Multiplier = authoredBand0;
        surface.largeBand1Multiplier = authoredBand1;
        if (surface.underWater != authoredUnderWater) surface.underWater = authoredUnderWater;
        applied = false;
    }
}
