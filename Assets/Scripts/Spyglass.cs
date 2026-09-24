using DogWater;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public class Spyglass : IItem
{
    CinemachineCamera camera;
    Fog fog;

    // The spyglass's own global fog Volume, above the sky (0) and weather (10/20) Volumes, so the zoom
    // fog is not overridden by them. Created at runtime; this class is the only writer of its profile.
    const float fogVolumePriority = 30f;
    Volume fogVolume;
    VolumeProfile fogProfile;
    bool fogBaseRead;

    public Spyglass()
    {
        //camera = Camera.main;
        //_Sky = SkyboxController.Instance._Sky;
    }
    private bool _equipped = false;
    private bool _interacting = false;
    public bool Equipped { get => _equipped; set => _equipped = value; }
    public bool Interacting { get => _interacting; set => _interacting = value; }

    float maxZoom = -1;
    float minZoom = 0;

    float currentZoom = float.MaxValue;
    float baseZoom;
    float basemeanFreePath;
    float baseMouseSens;
    FirstPersonController controller;
    // True only after an Equip found everything zoom needs; guards Interact and the reset.
    bool zoomReady;
    public void Equip(FirstPersonController controller)
    {
        Equipped = true;
        zoomReady = false;
        if (controller == null)
        {
            Debug.LogWarning("Spyglass: equipped without a FirstPersonController, zoom disabled.");
            return;
        }
        if (camera == null)
            camera = CinemachineCore.GetVirtualCamera(0) as CinemachineCamera;
        if (camera == null)
        {
            Debug.LogWarning("Spyglass: no CinemachineCamera at virtual camera index 0, zoom disabled.");
            return;
        }
        if (fogVolume == null)
            CreateFogVolume();


        baseZoom = camera.Lens.FieldOfView;
        minZoom = baseZoom;
        if (currentZoom == float.MaxValue)
            currentZoom = baseZoom;
        baseMouseSens = controller.RotationSpeed;
        this.controller = controller;
        zoomReady = true;
        Debug.LogError("Equipped " + this);
        //oyuncuya geri çağrı yapıcak , elde gözükecek , use1,use2 ve unequip fonskiyonlarının kullanımınmı açacak
    }

    public void Interact()
    {
        // camera can also be destroyed after Equip (scene change), Unity's == null catches that.
        if (!zoomReady || camera == null)
            return;
        // First frame of a zoom: our Volume is still at weight 0, so the camera's stack shows the real fog.
        if (!Interacting)
            ReadFogBase();
        Interacting = true;
        var Lens = camera.Lens;
        currentZoom = Mathf.Clamp(currentZoom, maxZoom, minZoom);
        if (fogBaseRead && fogVolume != null)
        {
            float scaledMeanFreePath = basemeanFreePath * Mathf.Pow(baseZoom / Lens.FieldOfView, 0.3f);
            fog.meanFreePath.value = scaledMeanFreePath;
            fogVolume.weight = 1f;
        }
        Lens.FieldOfView = Mathf.Lerp(Lens.FieldOfView, currentZoom, Time.deltaTime * 10);
        camera.Lens = Lens;
        controller.RotationSpeed = baseMouseSens * Mathf.Pow(Lens.FieldOfView / baseZoom, 1);
    }
    public void UnInteract()
    {
        if (Interacting)
        {
            Interacting = false;
            _resetZoomAndStuff();
        }

    }


    public void UnEquip()
    {
        _resetZoomAndStuff();

    }

    public void Use1()
    {
        Debug.LogError("Using1 " + currentZoom);
        //currentZoom/(baseZoom*5)
        currentZoom -= 0.1f + currentZoom / (baseZoom * 5);
    }

    public void Use2()
    {
        Debug.LogError("Using2 " + currentZoom);

        currentZoom += 0.1f + currentZoom / (baseZoom * 5);
    }
    private void _resetZoomAndStuff()
    {
        if (fogVolume != null)
            fogVolume.weight = 0f;
        if (!zoomReady)
            return;
        if (camera != null)
        {
            var Lens = camera.Lens;
            Lens.FieldOfView = baseZoom;
            camera.Lens = Lens;
        }
        if (controller != null)
            controller.RotationSpeed = baseMouseSens;
    }

    private void CreateFogVolume()
    {
        // Created once per spyglass and reused if the Volume's GameObject is destroyed with its scene.
        if (fogProfile == null)
        {
            fogProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            fog = fogProfile.Add<Fog>();
            fog.meanFreePath.overrideState = true;
            fog.maxFogDistance.overrideState = true;
            fog.maxFogDistance.value = 5000.0f;
        }

        GameObject volumeObject = new GameObject("SpyglassFogVolume");
        fogVolume = volumeObject.AddComponent<Volume>();
        fogVolume.isGlobal = true;
        fogVolume.priority = fogVolumePriority;
        fogVolume.weight = 0f;
        // sharedProfile is safe here: the profile is a runtime instance, not a Project asset.
        // Never use fogVolume.profile, it would clone the profile and ignore our writes to fog.
        fogVolume.sharedProfile = fogProfile;
    }

    private void ReadFogBase()
    {
        fogBaseRead = false;
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
            return;

        // The camera's blended Volume stack = sky + weather, including any fade in progress.
        // Only valid while our Volume is at weight 0, otherwise we would read back our own value.
        Fog blendedFog = HDCamera.GetOrCreate(mainCamera).volumeStack.GetComponent<Fog>();
        if (blendedFog == null)
            return;

        basemeanFreePath = blendedFog.meanFreePath.value;
        fogBaseRead = true;
    }
}