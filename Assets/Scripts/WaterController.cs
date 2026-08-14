using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

public class WaterController : NetworkBehaviour
{
    public NetworkVariable<Vector3> wind = new NetworkVariable<Vector3>(Vector3.forward, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public Vector3 editorWind;
    public WaterSurface targetSurface = null;

    // Internal search params
    public WaterSearchParameters searchParameters = new WaterSearchParameters();
    public WaterSearchResult searchResult = new WaterSearchResult();

    public static WaterController Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        if (IsServer)
        {
            wind.Value = Vector3.forward;
        }

        if(!IsOwner)
            return;
    }
    public Vector3? GetWave(Vector3 position)
    {
        if (targetSurface != null)
        {
            // Build the search parameters
            searchParameters.startPositionWS = searchResult.candidateLocationWS;
            searchParameters.targetPositionWS = position;
            searchParameters.error = 0.01f;
            searchParameters.maxIterations = 20;

            // Do the search
            if (targetSurface.ProjectPointOnWaterSurface(searchParameters, out searchResult))
            {
                return searchResult.projectedPositionWS;
            }
            else Debug.LogError("Can't Find Projected Position");
        }
        return null;
    }

    // Update is called once per frame
    void Update()
    {   
        //TODO chunk hareket kondisyonu , geminin önceki chunk değişiklinde kaydedilen pozisyonundan belli
        //bir miktarda uzaklaşması olabilir.
        //hareket edilen yöne göre yaklaşılan ve uzaklaşılan parçaların tespit edilip ; 
        // uzaklaşan parçaların yeni konumunun yaklaşılan parçaların konumu ve 
        // parça büyüklüğüne göre hesaplanması gerekli.
        //


        if(IsServer)
        {
            wind.Value = editorWind;
        }
        
        
       
    }
}
