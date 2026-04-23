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
        m_FirstPersonController.enabled = false;
        m_PlayerInput.enabled = false;
        m_CharacterController.enabled = false;

    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (IsOwner)
        {
            m_StarterAssetsInputs.enabled = true;
            m_FirstPersonController.enabled = true;
            m_PlayerInput.enabled = true;
            m_CharacterController.enabled = true;
 
        }
    }

}