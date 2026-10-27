using UnityEngine;

// Base for anything the weather drives (rain, lightning, later sound...). Put a subclass on the root
// of an effect prefab and list it in WeatherEffects. Swapping the visual means swapping the prefab -
// the weather code never changes. A new kind of asset (e.g. a VFX Graph effect) only needs one small
// subclass that translates SetIntensity/Trigger into that asset's own controls.
// Local visuals only: nothing here may affect gameplay or be networked.
public abstract class WeatherEffect : MonoBehaviour
{
    // 0 = off, 1 = the strongest this effect gets. Called only when the value changes.
    public abstract void SetIntensity(float intensity);

    // One-shot event, e.g. a lightning strike. Most effects ignore it.
    public virtual void Trigger() { }
}
