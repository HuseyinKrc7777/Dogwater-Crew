using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

public class WaterController : NetworkBehaviour
{
    public NetworkVariable<Vector3> wind = new NetworkVariable<Vector3>(Vector3.forward, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public Vector3 editorWind;

    // Ocean current as a world-space velocity (units/s, horizontal). Read by BoatMovement.
    // editorCurrent is the Inspector fallback, like editorWind; the weather system will drive it later.
    public NetworkVariable<Vector3> current = new NetworkVariable<Vector3>(Vector3.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public Vector3 editorCurrent;

    // Server-only override from the weather system (WeatherManager). While set, it replaces
    // editorWind / editorCurrent; when cleared (or no weather system exists) the editor values apply.
    private bool hasWeatherWind;
    private Vector3 weatherWind;
    private bool hasWeatherCurrent;
    private Vector3 weatherCurrent;

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

            // Never inherit a previous session's weather.
            hasWeatherWind = false;
            weatherWind = Vector3.zero;
            hasWeatherCurrent = false;
            weatherCurrent = Vector3.zero;
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

    public void SetWeatherWind(Vector3 value)
    {
        if (!IsServer || !IsFinite(value)) return;
        weatherWind = Vector3.ClampMagnitude(value, 10f);
        hasWeatherWind = true;
    }

    public void ClearWeatherWind()
    {
        if (!IsServer) return;
        hasWeatherWind = false;
    }

    public void SetWeatherCurrent(Vector3 value)
    {
        if (!IsServer || !IsFinite(value)) return;
        value.y = 0f;
        weatherCurrent = Vector3.ClampMagnitude(value, 10f);
        hasWeatherCurrent = true;
    }

    public void ClearWeatherCurrent()
    {
        if (!IsServer) return;
        hasWeatherCurrent = false;
    }

    private static bool IsFinite(Vector3 v)
    {
        return !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z)
            && !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);
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
            // NetworkVariable only marks itself dirty when the value actually changes.
            wind.Value = hasWeatherWind ? weatherWind : editorWind;
            current.Value = hasWeatherCurrent ? weatherCurrent : new Vector3(editorCurrent.x, 0f, editorCurrent.z);
        }
        
        
       
    }
}
