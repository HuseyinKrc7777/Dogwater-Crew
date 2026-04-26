using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class IslandSceneChanger : MonoBehaviour
{
    [SerializeField] public string IslandName;
    private bool _isTransitioning = false;


    private void OnTriggerEnter(Collider other)
    {
        if(_isTransitioning == true)
        return;

        var player = other.GetComponentInParent<ClientPlayerMove>();

        if(player != null && player.IsOwner)
        {
            RequestSceneLoadServerRpc(IslandName);
        }

    }

    [ServerRpc]
    private void RequestSceneLoadServerRpc(string sceneName)
    {
        if (_isTransitioning || sceneName == null) return;
        
        _isTransitioning = true;

        NetworkManager.Singleton.SceneManager.LoadScene(IslandName, LoadSceneMode.Single);
    }
    
}
