using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using DogWater;

public class ClientPlayerMove : NetworkBehaviour
{
    [SerializeField] private PlayerInput m_PlayerInput;
    [SerializeField] private PlayerInputs m_StarterAssetsInputs;
    [SerializeField] private FirstPersonController m_FirstPersonController;
    [SerializeField] private CharacterController m_CharacterController;

    private Renderer[] playerRenderers;
    private bool[] rendererEnabledStates;
    private bool sceneEventSubscribed;
    private bool gameplayReady;

    private void Awake()
    {
        m_StarterAssetsInputs.enabled = false;
        m_PlayerInput.enabled = false;
        m_CharacterController.enabled = false;
        CacheAndHidePlayerRenderers();

    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsOwner)
            m_FirstPersonController.enabled = false;

        SubscribeToSceneEvents();

        string gameplaySceneName = SessionManager.GameplaySceneName;

        if (!string.IsNullOrEmpty(gameplaySceneName)
            && NetworkManager.IsConnectedClient
            && SceneManager.GetActiveScene().name == gameplaySceneName)
            SetGameplayReady();
    }

    public override void OnNetworkDespawn()
    {
        UnsubscribeFromSceneEvents();
        base.OnNetworkDespawn();
    }

    private void SetGameplayReady()
    {
        if (gameplayReady)
            return;

        gameplayReady = true;
        RestorePlayerRenderers();

        if (IsOwner)
        {
            // Oyuncuların çakışmasını KESİN olarak önlemek için her oyuncuya özel ID'sine (OwnerClientId) göre
            // sabit bir aralıkla yan yana doğmalarını sağlıyoruz.
            // Host (ID: 0) tam merkezde (0) doğarken, 1. Client 2 birim yanda, 2. Client 4 birim yanda doğacaktır.
            float offsetDistance = 1.0f;
            Vector3 spawnOffset = new Vector3(OwnerClientId * offsetDistance, 0f, 0f);
            transform.position += spawnOffset;

            m_FirstPersonController._verticalVelocity = 0f;
            m_FirstPersonController.enabled = true;
            m_StarterAssetsInputs.enabled = true;
            m_PlayerInput.enabled = true;
            m_CharacterController.enabled = true;
        }

        UnsubscribeFromSceneEvents();
    }

    private void HandleSceneEvent(SceneEvent sceneEvent)
    {
        if (gameplayReady)
            return;

        string gameplaySceneName = SessionManager.GameplaySceneName;
        if (string.IsNullOrEmpty(gameplaySceneName))
            return;

        bool gameplaySceneLoaded =
            sceneEvent.SceneEventType == SceneEventType.LoadEventCompleted
            && sceneEvent.SceneName == gameplaySceneName
            && SceneManager.GetActiveScene().name == gameplaySceneName;

        bool localSynchronizationCompleted =
            sceneEvent.SceneEventType == SceneEventType.SynchronizeComplete
            && sceneEvent.ClientId == NetworkManager.LocalClientId
            && SceneManager.GetActiveScene().name == gameplaySceneName;

        if (gameplaySceneLoaded || localSynchronizationCompleted)
            SetGameplayReady();
    }

    private void SubscribeToSceneEvents()
    {
        if (sceneEventSubscribed || NetworkManager.SceneManager == null)
            return;

        NetworkManager.SceneManager.OnSceneEvent += HandleSceneEvent;
        sceneEventSubscribed = true;
    }

    private void UnsubscribeFromSceneEvents()
    {
        if (!sceneEventSubscribed)
            return;

        if (NetworkManager != null && NetworkManager.SceneManager != null)
            NetworkManager.SceneManager.OnSceneEvent -= HandleSceneEvent;

        sceneEventSubscribed = false;
    }

    private void CacheAndHidePlayerRenderers()
    {
        playerRenderers = GetComponentsInChildren<Renderer>(true);
        rendererEnabledStates = new bool[playerRenderers.Length];

        for (int i = 0; i < playerRenderers.Length; i++)
        {
            rendererEnabledStates[i] = playerRenderers[i].enabled;
            playerRenderers[i].enabled = false;
        }
    }

    private void RestorePlayerRenderers()
    {
        for (int i = 0; i < playerRenderers.Length; i++)
        {
            if (playerRenderers[i] != null)
                playerRenderers[i].enabled = rendererEnabledStates[i];
        }
    }

}
