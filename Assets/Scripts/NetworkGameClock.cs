using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class NetworkGameClock : NetworkBehaviour
{
    public static NetworkGameClock Instance { get; private set; }

    [SerializeField] private bool persistAcrossScenes = true;
    [SerializeField] private float dayLengthSeconds = 600f;

    private NetworkVariable<double> elapsedGameSeconds = new NetworkVariable<double>(
        0d,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public double ElapsedGameSeconds => elapsedGameSeconds.Value;
    public float DayLengthSeconds => Mathf.Max(1f, dayLengthSeconds);
    public int CurrentDay => Mathf.FloorToInt((float)(elapsedGameSeconds.Value / DayLengthSeconds)) + 1;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Multiple NetworkGameClock instances found. Keeping the first instance.");
            enabled = false;
            return;
        }

        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        if (persistAcrossScenes)
        {
            DontDestroyOnLoad(gameObject);
        }
    }

    private void Update()
    {
        if (!IsServer)
        {
            return;
        }

        elapsedGameSeconds.Value += Time.deltaTime;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
