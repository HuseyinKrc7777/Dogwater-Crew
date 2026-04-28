using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class Cannon : NetworkBehaviour, IHandInput
{
    [Header("References")]
    [SerializeField] private GameObject cannonballPrefab;
    [SerializeField] private Transform spawnPoint;

    [Header("Settings")]
    [SerializeField] private float fireForce = 20f;
    
    [Header("Rotation Settings")]
    [SerializeField] private float maxYaw = 15f;       // Sağa/sola maksimum dönme açısı
    [SerializeField] private float minPitch = -5f;     // Aşağı maksimum eğilme açısı
    [SerializeField] private float maxPitch = 15f;     // Yukarı maksimum kalkma açısı
    [SerializeField] private float manualRotationSpeed = 100f;

    // Herkesin göreceği senkronize açı değerleri
    public NetworkVariable<float> cannonYaw = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<float> cannonPitch = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private Quaternion initialRotation;
    private bool isInteracting = false;

    void Awake()
    {
        // Topun sahnedeki ilk dönüş açısını baz alıyoruz
        initialRotation = transform.localRotation;
    }

    public void OnInteract(Player player)
    {
        isInteracting = true;
    }

    public void OnUnInteract(Player player)
    {
        isInteracting = false;
    }

    public void OnHandInput(float xValue, float yValue)
    {
        // FirstPersonController'dan gelen değerleri tersine çevirip dönme hızımızla çarpıyoruz
        float yawDelta = -xValue * manualRotationSpeed;
        float pitchDelta = yValue * manualRotationSpeed; 

        RotateCannonRpc(yawDelta, pitchDelta);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RotateCannonRpc(float yawDelta, float pitchDelta)
    {
        // Açıları sınırlar içinde (eser miktarda) tutuyoruz
        float newYaw = Mathf.Clamp(cannonYaw.Value + yawDelta, -maxYaw, maxYaw);
        float newPitch = Mathf.Clamp(cannonPitch.Value + pitchDelta, minPitch, maxPitch);
        
        cannonYaw.Value = newYaw;
        cannonPitch.Value = newPitch;
    }

    void Update()
    {
        // Topun görüntüsünü yavaş ve pürüzsüz bir şekilde senkronize edilen açıya doğru çevir
        Quaternion targetRotation = initialRotation * Quaternion.Euler(cannonPitch.Value, cannonYaw.Value, 0f);
        transform.localRotation = Quaternion.Lerp(transform.localRotation, targetRotation, Time.deltaTime * 10f);

        // YALNIZCA bu topla etkileşimde olan (tutunan) oyuncu Space'e basınca ateşleyebilir
        if (isInteracting && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            RequestFireRpc();
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestFireRpc(RpcParams rpcParams = default)
    {
        GameObject cannonballInstance = Instantiate(cannonballPrefab, spawnPoint.position, spawnPoint.rotation);

        if (cannonballInstance.TryGetComponent<Rigidbody>(out Rigidbody rb))
        {
            // Namlunun baktığı yöne (spawnPoint.forward) doğru fırlat!
            rb.linearVelocity = spawnPoint.forward * fireForce;
        }

        if (cannonballInstance.TryGetComponent<NetworkObject>(out NetworkObject netObj))
        {
            netObj.Spawn();
        }
    }
}