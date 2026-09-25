using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

// Scales the scene's sun by the weather's Sunlight value (1 = as authored in the scene, lower = sun
// behind clouds). It is the only runtime writer of the sun's intensity and sky-disk look -
// SkyboxController only rotates the sun - so the two never fight. Local visual; every peer applies
// the same value.
//
// The disk needs its own, much steeper curve: HDRP's physically based sky draws it with
// radiance = light intensity x surfaceTint / (disk solid angle, ~6e-5 sr for 0.5 deg), so even at 12 %
// light it is millions of times brighter than the sky and auto exposure shows a white dot. The disk
// (and its flare) therefore fades on a log scale and is fully hidden below diskHiddenAtOrBelow.
public class SunlightWeatherEffect : WeatherEffect
{
    [Tooltip("The scene's sun: the directional Light SkyboxController rotates. A scene object, so wire it on the prefab instance in each gameplay scene.")]
    [SerializeField] private Light sun;

    [Header("Sun disk in the sky")]
    [Tooltip("Sunlight at or above this shows the disk as authored.")]
    [Range(0f, 1f)][SerializeField] private float diskFullAtOrAbove = 0.95f;
    [Tooltip("Sunlight at or below this hides the disk and its flare completely.")]
    [Range(0f, 1f)][SerializeField] private float diskHiddenAtOrBelow = 0.6f;
    [Tooltip("Orders of magnitude the disk fades over between the two thresholds (it is extremely bright).")]
    [Min(1f)][SerializeField] private float diskFadeDecades = 7f;

    private HDAdditionalLightData sunData;
    private bool captured;
    private float authoredIntensity;
    private Color authoredSurfaceTint;
    private float authoredFlareMultiplier;

    private void Awake()
    {
        if (sun == null)
        {
            Debug.LogWarning("[Weather] SunlightWeatherEffect has no sun Light assigned; the sun will not dim with the weather.", this);
            return;
        }
        sunData = sun.GetComponent<HDAdditionalLightData>();
    }

    public override void SetIntensity(float intensity)
    {
        if (sun == null) return;   // Unity null: also catches a sun destroyed with its scene
        Capture();

        float sunlight = Mathf.Clamp01(intensity);
        sun.intensity = authoredIntensity * sunlight;

        if (sunData != null)
        {
            float disk = DiskScale(sunlight);
            sunData.surfaceTint = authoredSurfaceTint * disk;
            sunData.flareMultiplier = authoredFlareMultiplier * disk;
        }
    }

    // 1 above the upper threshold, 0 below the lower one, and an exponential fade in between so the
    // disk visibly dims instead of staying white until the last frame.
    private float DiskScale(float sunlight)
    {
        if (sunlight >= diskFullAtOrAbove) return 1f;
        if (sunlight <= diskHiddenAtOrBelow || diskFullAtOrAbove <= diskHiddenAtOrBelow) return 0f;
        float t = (sunlight - diskHiddenAtOrBelow) / (diskFullAtOrAbove - diskHiddenAtOrBelow);
        return Mathf.Pow(10f, -diskFadeDecades * (1f - t));
    }

    private void Capture()
    {
        if (captured) return;
        captured = true;
        authoredIntensity = sun.intensity;
        if (sunData != null)
        {
            authoredSurfaceTint = sunData.surfaceTint;
            authoredFlareMultiplier = sunData.flareMultiplier;
        }
    }

    private void OnDisable()
    {
        // Leave the sun as the scene authored it.
        if (!captured || sun == null) return;
        sun.intensity = authoredIntensity;
        if (sunData != null)
        {
            sunData.surfaceTint = authoredSurfaceTint;
            sunData.flareMultiplier = authoredFlareMultiplier;
        }
    }
}
