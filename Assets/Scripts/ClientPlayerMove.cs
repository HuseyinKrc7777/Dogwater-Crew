using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using DogWater;

public class ClientPlayerMove : NetworkBehaviour
{
    [SerializeField] private PlayerInput m_PlayerInput;
    [SerializeField] private PlayerInputs m_StarterAssetsInputs;
    [SerializeField] private FirstPersonController m_FirstPersonController;
    [SerializeField] private CharacterController m_CharacterController;


    private void Awake()
    {
        m_StarterAssetsInputs.enabled = false;
        m_PlayerInput.enabled = false;
        m_CharacterController.enabled = false;

    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (IsOwner)
        {
            // Oyuncuların çakışmasını KESİN olarak önlemek için her oyuncuya özel ID'sine (OwnerClientId) göre
            // sabit bir aralıkla yan yana doğmalarını sağlıyoruz.
            // Host (ID: 0) tam merkezde (0) doğarken, 1. Client 2 birim yanda, 2. Client 4 birim yanda doğacaktır.
            float offsetDistance = 1.0f;
            Vector3 spawnOffset = new Vector3(OwnerClientId * offsetDistance, 0f, 0f);
            transform.position += spawnOffset;

            m_StarterAssetsInputs.enabled = true;
            m_PlayerInput.enabled = true;
            m_CharacterController.enabled = true;
        }
    }

}