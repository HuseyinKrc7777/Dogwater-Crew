using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using DogWater;
using SunCalcSharp;
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
    public Vector3 LookDirection {get => controller.CinemachineCameraTarget.transform.forward;}
    [SerializeField] public TextMeshPro nameDisplay;
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
        LanternObject.SetActive(false);
        FishingRodObject.SetActive(false);
        bobber.SetActive(false);

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
            
            Items.Add(new Spyglass());

            
            Items.Add(new EquipableCompass());

            Items.Add(new Sextant());
            diary = new Diary();
            Items.Add(diary);

            rod = new FishingRod();
            Items.Add(rod);

            Items.Add(new Lantern());

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
    public TMP_InputField diaryUi;
    public Diary diary;
    [Rpc(SendTo.Everyone)]
    public void CompassRpc(bool state)
    {
        ///animasyon manimasyon filan fişman da oynatılabilir burada
        compassObject.SetActive(state);
    }
    public GameObject LanternObject;
    [Rpc(SendTo.Everyone)]
    internal void LanternRpc(bool v)
    {
        LanternObject.SetActive(v);
    }
    public GameObject FishingRodObject;
    private FishingRod rod;
    public GameObject bobber;
    [Rpc(SendTo.Everyone)]
    internal void FishingRodEquipRpc(bool v)
    {
        FishingRodObject.SetActive(v);
    }
    private boberController bober;
    [Rpc(SendTo.NotOwner)]
    internal void FishingRodCastRpc(float charge,Vector3 dir)
    {
        bober= UnityEngine.Object.Instantiate(bobber).GetComponent<boberController>();
        bober.transform.position = bobber.transform.position;
        bober.gameObject.SetActive(true);
        bober._rigidbody = bober.GetComponent<Rigidbody>();
        bober._rigidbody.AddForce(dir.normalized * charge*15,ForceMode.Impulse);
        //rod.CastRod(charge,dir);
    }
    [Rpc(SendTo.NotOwner)]
    internal void FishingRodReelRpc(Vector3 force)
    {
        bober._rigidbody.AddForce(force, ForceMode.Force);
    }
    [Rpc(SendTo.NotOwner)]
    internal void FishingRodFlickRpc(Vector3 force)
    {
        bober._rigidbody.AddForce(force, ForceMode.Impulse);
        
    }
    [Rpc(SendTo.NotOwner)]
    internal void DestroyBobberRpc()
    {
        Destroy(bober.gameObject);
        
    }
    [Rpc(SendTo.NotOwner)]
    internal void HookRpc(ulong objectid)
    {
        IHoldableItem obj = NetworkManager.Singleton.SpawnManager.SpawnedObjects[(ulong)objectid].GetComponent<IHoldableItem>();
        obj.spawnedMesh.transform.parent = bober.transform;
    }

}



