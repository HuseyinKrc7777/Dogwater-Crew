using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class IslandSceneChanger : NetworkBehaviour
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
            _isTransitioning = true;
            RequestSceneLoadServerRpc(IslandName);
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestSceneLoadServerRpc(string sceneName)
    {
        if (!IsServer || string.IsNullOrEmpty(sceneName)) return;
        
        NetworkManager.Singleton.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }
    
}
