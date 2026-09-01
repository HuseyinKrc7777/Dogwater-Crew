using System;
using System.Collections.Generic;
using DogWater;
using TMPro;
using Unity.Cinemachine;
using Unity.Collections;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Diagnostics;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using WebSocketSharp;

public class Player : NetworkBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public FirstPersonController controller;
    [SerializeField] public TextMeshPro nameDisplay;
    private Spyglass spyglass;
    private EquipableCompass compass;
    public List<IItem> Items = new();
    //TODO oyuncu isminin client dan değiştirilebiliyor olması güvenlik açığı sayılabilir.
    public NetworkVariable<FixedString64Bytes> PlayerName = new NetworkVariable<FixedString64Bytes>(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    void Start()
    {
        controller = GetComponent<FirstPersonController>();
        originalTextRotation = nameDisplay.transform.eulerAngles;
    }



    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        PlayerName.OnValueChanged += OnNameChange;
        compassObject.SetActive(false);

        if (IsOwner)
        {
            //burada oyuncu id si filan şey olabilir eğer boş ise
            if (SessionManager.LocalPlayerName.Trim().IsNullOrEmpty())
                SessionManager.LocalPlayerName = "Player";
            PlayerName.Value = SessionManager.LocalPlayerName;
            SyncNames();
        }

    }
    protected override void OnNetworkPostSpawn()
    {
        base.OnNetworkPostSpawn();

        if(IsOwner)
        {
             spyglass = new();
            Items.Add(spyglass);

            compass = new();
            Items.Add(compass);
        }
       
        
    }
    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        PlayerName.OnValueChanged -= OnNameChange;

    }

    private void OnNameChange(FixedString64Bytes previousValue, FixedString64Bytes newValue)
    {
        UpdateNameDisplay();
    }


    private void SyncNames()
    {
        GameObject[] allPlayerObjects = GameObject.FindGameObjectsWithTag("Player");
        foreach (GameObject obj in allPlayerObjects)
        {
            obj.GetComponent<Player>().UpdateNameDisplay();
        }
    }
    public void UpdateNameDisplay()
    {
        nameDisplay.text = PlayerName.Value.ToString();
    }


    // Update is called once per frame
    void Update()
    {

    }
    private Vector3 originalTextRotation;
    void LateUpdate()
    {
        if (Camera.main == null) return;

        // Make the text look at the camera
        nameDisplay.transform.LookAt(Camera.main.transform);

        // LookAt makes the text face *away* from the camera, so flip 180° on Y
        nameDisplay.transform.Rotate(0, 180, 0);

        // Re-lock axes if enabled
        Vector3 current = nameDisplay.transform.eulerAngles;
        nameDisplay.transform.eulerAngles = new Vector3(
            originalTextRotation.x,
            current.y,
            originalTextRotation.z
        );
    }
    public GameObject compassObject;
    [Rpc(SendTo.Everyone)]
    public void CompassRpc(bool state)
    {
        ///animasyon manimasyon filan fişman da oynatılabilir burada
        compassObject.SetActive(state);
    }
}
public interface IItem
{
    public bool Equipped { get; set; }
    public bool Interacting { get; set; }

    public void Equip(FirstPersonController controller);
    public void UnEquip();
    public void Interact();
    public void UnInteract();

    public void Use1();
    public void Use2();


}
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
            VolumeProfile profile = SkyboxController.Instance._Volume.sharedProfile;
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
        if(currentZoom==float.MaxValue)
            currentZoom=baseZoom;
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
        currentZoom -= 0.1f + currentZoom/(baseZoom*5);
    }

    public void Use2()
    {
        Debug.LogError("Using2 " + currentZoom);

        currentZoom += 0.1f + currentZoom/(baseZoom*5);
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

public class EquipableCompass : IItem
{
    private bool _equipped;
    private bool _interacting;
    private Player player;

    [SerializeField] Transform CompassObject;

    public bool Equipped { get => _equipped; set => _equipped = value; }
    public bool Interacting { get => _interacting; set => _interacting = value; }
    public void Equip(FirstPersonController controller)
    {
        //burada 
        player = controller.GetComponent<Player>();
        player.CompassRpc(true);
        //throw new NotImplementedException();
    }

    public void Interact()
    {
        //TODO pusulayı ekrana yaklaştırıp açıları daha okunablir yapılacak
        //throw new NotImplementedException();
    }

    public void UnEquip()
    {
        player.CompassRpc(false);

        //throw new NotImplementedException();
    }

    public void UnInteract()
    {
        //throw new NotImplementedException();
    }

    public void Use1()
    {
        //throw new NotImplementedException();
    }

    public void Use2()
    {
        //throw new NotImplementedException();
    }
}