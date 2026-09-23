using DogWater;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public class Spyglass : IItem
{
    CinemachineCamera camera;
    Fog fog;

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
    public void Equip(FirstPersonController controller)
    {
        Equipped = true;
        if (camera == null)
            camera = (CinemachineCamera)CinemachineCore.GetVirtualCamera(0);
        if (fog == null)
        {
            // The sky Volume's runtime copy, not sharedProfile: writing fog into the asset made the zoom
            // changes permanent after Play Mode (and SkyboxController renders the copy now anyway).
            VolumeProfile profile = SkyboxController.Instance._Volume.profile;
            if (profile.TryGet<Fog>(out var Fog))
            {
                fog = Fog;
                fog.meanFreePath.overrideState = true;
                fog.maxFogDistance.value = 5000.0f;
                basemeanFreePath = fog.meanFreePath.value;
            }
        }


        baseZoom = camera.Lens.FieldOfView;
        minZoom = baseZoom;
        if (currentZoom == float.MaxValue)
            currentZoom = baseZoom;
        baseMouseSens = controller.RotationSpeed;
        this.controller = controller;
        Debug.LogError("Equipped " + this);
        //oyuncuya geri çağrı yapıcak , elde gözükecek , use1,use2 ve unequip fonskiyonlarının kullanımınmı açacak
    }

    public void Interact()
    {
        Interacting = true;
        var Lens = camera.Lens;
        currentZoom = Mathf.Clamp(currentZoom, maxZoom, minZoom);
        float scaledMeanFreePath = basemeanFreePath * Mathf.Pow(baseZoom / Lens.FieldOfView, 0.3f);
        fog.meanFreePath.value = scaledMeanFreePath;
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
        var Lens = camera.Lens;
        Lens.FieldOfView = baseZoom;
        camera.Lens = Lens;
        fog.meanFreePath.value = basemeanFreePath;
        controller.RotationSpeed = baseMouseSens;
    }
}