using System;
using System.Collections.Generic;
using DogWater;
using TMPro;
using Unity.Cinemachine;
using Unity.Collections;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEditor;
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

        if (IsOwner)
        {
            spyglass = new();
            Items.Add(spyglass);

            compass = new();
            Items.Add(compass);

            Items.Add(new Sextant());
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

public class Sextant : IItem
{
    private bool _equipped;
    private bool _interacting;
    private Player player;
    CinemachineCamera cameraCinemachine;
    Camera camera;

    private Camera leftCamera;
    private Camera rightCamera;

    private CinemachineCamera leftCM;
    private CinemachineCamera rightCM;

    GameObject leftAimObject;
    GameObject rightAimObject;
    public bool Equipped { get => _equipped; set => _equipped = value; }
    public bool Interacting { get => _interacting; set => _interacting = value; }

    public void Equip(FirstPersonController controller)
    {
        player = controller.GetComponent<Player>();
        if (camera == null)
        {
            cameraCinemachine = (CinemachineCamera)CinemachineCore.GetVirtualCamera(0);
            camera = Camera.main;
        }



        // Duplicate Unity camera
        leftCamera = UnityEngine.Object.Instantiate(camera);
        rightCamera = UnityEngine.Object.Instantiate(camera);
        leftCamera.GetComponent<AudioListener>().enabled = false;

        leftCamera.name = "Camera_Left";
        rightCamera.name = "Camera_Right";

        // Duplicate Cinemachine cameras
        leftCM = UnityEngine.Object.Instantiate(cameraCinemachine);
        rightCM = UnityEngine.Object.Instantiate(cameraCinemachine);

        leftCM.name = "CM_Left";
        rightCM.name = "CM_Right";



        leftAimObject = new GameObject("LeftAimTarget");
        rightAimObject = new GameObject("RightAimTarget");
        leftAimObject.tag = "CinemachineTarget";
        rightAimObject.tag = "CinemachineTarget";


        Transform leftAimTarget = leftAimObject.transform;
        Transform rightAimTarget = rightAimObject.transform;

        Transform firstPersonTarget = controller.CinemachineCameraTarget.transform;
        leftAimTarget.SetParent(firstPersonTarget, false);
        rightAimTarget.SetParent(firstPersonTarget, false);

        leftAimTarget.localRotation =
            Quaternion.Euler(0f, -18, 0f);

        rightAimTarget.localRotation =
            Quaternion.Euler(0f, 18, 0f);


        leftCM.Follow = firstPersonTarget;
        rightCM.Follow = firstPersonTarget;

        rightCM.Target.TrackingTarget = rightAimTarget;
        leftCM.Target.TrackingTarget = leftAimTarget;

        leftCM.Target.LookAtTarget = leftAimTarget;
        rightCM.Target.LookAtTarget = rightAimTarget;
        if (leftCM.GetComponent<CinemachineRotationComposer>() == null)
        {
            leftCM.gameObject.AddComponent<CinemachineRotationComposer>();
        }

        if (rightCM.GetComponent<CinemachineRotationComposer>() == null)
        {
            rightCM.gameObject.AddComponent<CinemachineRotationComposer>();
        }
        
       

        CinemachineBrain leftBrain =
            leftCamera.GetComponent<CinemachineBrain>();

        CinemachineBrain rightBrain =
            rightCamera.GetComponent<CinemachineBrain>();


        if (leftBrain == null)
        {
            leftBrain = leftCamera.gameObject.AddComponent<CinemachineBrain>();
        }

        if (rightBrain == null)
        {
            rightBrain = rightCamera.gameObject.AddComponent<CinemachineBrain>();
        }


        leftCM.OutputChannel = OutputChannels.Channel01;
        rightCM.OutputChannel = OutputChannels.Channel02;


        leftBrain.ChannelMask = OutputChannels.Channel01;
        rightBrain.ChannelMask = OutputChannels.Channel02;


        leftCamera.rect = new Rect(0f, 0f, 0.5f, 1f);
        rightCamera.rect = new Rect(0.5f, 0f, 0.5f, 1f);


        leftCM.enabled = true;
        rightCM.enabled = true;

        leftCM.Priority = 100;
        rightCM.Priority = 100;

        rightCamera.gameObject.SetActive(false);
        rightCM.gameObject.SetActive(false);

        leftCamera.gameObject.SetActive(false);
        leftCM.gameObject.SetActive(false);


    }

    public void Interact()
    {
        //player ' dan görüntüsel fonksiyonlar ve rpc ler çalıştırılacak
        if(!Interacting)
            changeCam();
        Interacting = true;
       
    }

    public void UnEquip()
    {
        UnityEngine.Object.Destroy(leftCamera.gameObject);
        UnityEngine.Object.Destroy(leftCM.gameObject);
        UnityEngine.Object.Destroy(rightCamera.gameObject);
        UnityEngine.Object.Destroy(rightCM.gameObject);
        UnityEngine.Object.Destroy(leftAimObject);
        UnityEngine.Object.Destroy(rightAimObject);

        camera.gameObject.SetActive(true);
        cameraCinemachine.gameObject.SetActive(true);

    }

    public void UnInteract()
    {
        if(Interacting)
            changeCam();
        Interacting = false;

    }
    private void changeCam()
    {
        camera.gameObject.SetActive(!camera.gameObject.activeSelf);
        cameraCinemachine.gameObject.SetActive(!cameraCinemachine.gameObject.activeSelf);

        leftCamera.gameObject.SetActive(!leftCamera.gameObject.activeSelf);
        leftCM.gameObject.SetActive(!leftCM.gameObject.activeSelf);

        rightCamera.gameObject.SetActive(!rightCamera.gameObject.activeSelf);
        rightCM.gameObject.SetActive(!rightCM.gameObject.activeSelf);
    }
    public void Use1()
    {

        rightAimObject.transform.Rotate(Vector3.right,-0.1f);
    }

    public void Use2()
    {
        rightAimObject.transform.Rotate(Vector3.right,0.1f);


        
    }
}