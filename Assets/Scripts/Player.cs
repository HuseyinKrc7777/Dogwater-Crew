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
}


