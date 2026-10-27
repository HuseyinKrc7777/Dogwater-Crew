using DogWater;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class Cannon : MonoBehaviour, IHandInput
{
    public int index;
    public CannonNetworkController controller;
    [Header("References")]
    [SerializeField] public GameObject cannonballPrefab;
    [SerializeField] public Transform spawnPoint;

    [Header("Settings")]
    [SerializeField] public float fireForce = 100f;
    
    [Header("Rotation Settings")]
    [SerializeField] public float maxYaw = 15f;       // Sağa/sola maksimum dönme açısı
    [SerializeField] public float minPitch = -5f;     // Aşağı maksimum eğilme açısı
    [SerializeField] public float maxPitch = 15f;     // Yukarı maksimum kalkma açısı
    [SerializeField] public float manualRotationSpeed = 100f;

    // Herkesin göreceği senkronize açı değerleri
    

    private Quaternion initialRotation;
    private bool isInteracting = false;
    FirstPersonController PlayerController;

    void Awake()
    {
        // Topun sahnedeki ilk dönüş açısını baz alıyoruz
        initialRotation = transform.localRotation;
    }

    public void OnInteract(Player player)
    {
        isInteracting = true;
        PlayerController = player.GetComponent<FirstPersonController>();
    }

    public void OnUnInteract(Player player)
    {
        PlayerController.cameraRotationAllowed = true;
        isInteracting = false;
        PlayerController=null;
    }

    public void OnHandInput(float xValue, float yValue)
    {
        // FirstPersonController'dan gelen değerleri tersine çevirip dönme hızımızla çarpıyoruz
        PlayerController.cameraRotationAllowed = false;

        float yawDelta = -xValue * manualRotationSpeed;
        float pitchDelta = yValue * manualRotationSpeed; 

        controller.RotateCannonRpc(index,yawDelta, pitchDelta);
    }

   
    void Update()
    {
        if(holdingRight==false && PlayerController!=null)
            PlayerController.cameraRotationAllowed = false;
        else
            holdingRight = false;
        // Topun görüntüsünü yavaş ve pürüzsüz bir şekilde senkronize edilen açıya doğru çevir
        Quaternion targetRotation = initialRotation * Quaternion.Euler(controller.cannonPitch.Value, controller.cannonYaw.Value, 0f);
        transform.localRotation = Quaternion.Lerp(transform.localRotation, targetRotation, Time.deltaTime * 10f);

        // YALNIZCA bu topla etkileşimde olan (tutunan) oyuncu Space'e basınca ateşleyebilir
        /*if (isInteracting && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            RequestFireRpc();
        }*/
    }

   
    bool holdingRight = false;
    public void OnRightHandInput(float xValue, float yValue)
    {
        holdingRight = true;
        PlayerController.cameraRotationAllowed = true;
        

        
        //throw new System.NotImplementedException();
    }

    public void OnButtonInput()
    {
        controller.RequestFireRpc(index);
    }
}