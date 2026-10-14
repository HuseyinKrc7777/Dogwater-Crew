using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

// Clouds drift with the weather wind. Runs on every peer and decides nothing: it reads the synced
// WaterController.wind (the same vector the sails and the flag use), so the clouds move the way the flag
// points, speed up in stronger weather and turn when the wind turns. No network traffic.
//
// HDRP 17.3 (CloudLayer.cs, CloudLayerRenderer.cs, CloudLayerCommon.hlsl): the cloud layer is a 2D cloud
// map on the sky dome. With distortionMode = Procedural the shader samples it twice, offset along the
// scroll direction, and crossfades the two - the clouds appear to drift. scrollOrientation/scrollSpeed
// default to Global mode, i.e. they read VisualEnvironment.windOrientation/windSpeed, which nothing else
// in this project drives (only the cloud layer, volumetric clouds - disabled - and HDRI sky read them).
// Scrolling is not part of the cloud bake hash, so it costs one extra texture sample, no re-bake.
//
// Owns one runtime global Volume (never a Project asset, so writing its sharedProfile is safe), above the
// weather Volumes (10/20) and below the spyglass fog Volume (30). Disabling this component sets its weight
// to 0, which hands the clouds back to the scene's profiles (static, as authored).
//
// Known limit: HDRP accumulates the scroll position from each machine's own render time, so peers see the
// same direction and speed but the clouds are not at exactly the same place. Cosmetic only.
public class WeatherCloudWind : MonoBehaviour
{
    [Tooltip("Cloud speed in km/h per unit of weather wind. 90 = about 55-90 km/h in Clear, 135-200 km/h in Storm " +
             "(chosen in Play Mode 2026-10-09 so the drift is visible).")]
    [Min(0f)][SerializeField] private float kmhPerWindUnit = 90f;
    [Tooltip("Priority of this component's global Volume: above the weather Volumes (10/20), below the spyglass fog Volume (30).")]
    [SerializeField] private float volumePriority = 25f;

    private Volume volume;
    private VolumeProfile profile;
    private VisualEnvironment visualEnvironment;

    private void OnEnable()
    {
        if (volume == null) CreateVolume();
        // Weight stays 0 until a wind is known, so the clouds never jump to a default direction.
        volume.weight = 0f;
    }

    private void Update()
    {
        // Unity null: WaterController never clears its static Instance, so a destroyed one can linger.
        WaterController water = WaterController.Instance;
        if (water == null || !water.IsSpawned)
        {
            volume.weight = 0f;
            return;
        }

        Vector3 wind = water.wind.Value;
        float speed = new Vector2(wind.x, wind.z).magnitude;
        if (speed > 1e-4f)
        {
            // The shader samples ahead along (-cos a, -sin a), so the clouds drift toward angle a,
            // measured from world +X toward +Z: the direction the wind blows toward.
            float angle = Mathf.Atan2(wind.z, wind.x) * Mathf.Rad2Deg;
            if (angle < 0f) angle += 360f;
            visualEnvironment.windOrientation.value = angle;
        }
        visualEnvironment.windSpeed.value = speed * kmhPerWindUnit;
        volume.weight = 1f;
    }

    private void OnDisable()
    {
        if (volume != null) volume.weight = 0f;
    }

    private void OnDestroy()
    {
        // The profile and its components are runtime instances: nothing else frees them.
        if (profile != null)
        {
            foreach (VolumeComponent component in profile.components)
            {
                Destroy(component);
            }
            Destroy(profile);
        }
        if (volume != null) Destroy(volume.gameObject);
    }

    private void CreateVolume()
    {
        profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.name = "WeatherCloudWindProfile";

        visualEnvironment = profile.Add<VisualEnvironment>(false);
        visualEnvironment.windOrientation.overrideState = true;
        visualEnvironment.windSpeed.overrideState = true;

        CloudLayer clouds = profile.Add<CloudLayer>(false);
        clouds.layerA.distortionMode.Override(CloudDistortionMode.Procedural);

        // Child of this object, so it lives and dies with the presentation (and with its scene).
        GameObject volumeObject = new GameObject("WeatherCloudWindVolume");
        volumeObject.transform.SetParent(transform, false);
        volume = volumeObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = volumePriority;
        volume.weight = 0f;
        // Never use volume.profile: it would clone the profile and ignore our writes.
        volume.sharedProfile = profile;
    }
}
