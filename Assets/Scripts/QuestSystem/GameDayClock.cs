using System;
using Unity.Netcode;
using UnityEngine;

// Server-driven in-game day counter. The elapsed time inside a day is a plain server-local float:
// only the day rollover itself is networked, so this writes a NetworkVariable a few times per hour
// instead of every frame.
public class GameDayClock : NetworkBehaviour
{
    public static GameDayClock Instance { get; private set; }

    [Tooltip("Real seconds per in-game day.")]
    [Min(1f)][SerializeField] public float dayLengthSeconds = 600f;

    private NetworkVariable<int> currentDay = new NetworkVariable<int>(
        1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public float dayTimer;

    public int CurrentDay => currentDay.Value;
    private DateTime StartDate = DateTime.UtcNow;

    public DateTime GetCurrentDate()
    {
        DateTime TempDate = StartDate;

        TempDate = TempDate.AddDays(CurrentDay - 1);
        TempDate = TempDate.AddSeconds(dayTimer * (86400f / dayLengthSeconds));
        return TempDate;
    }

    // Fires on every peer when the day rolls over. The UI uses this to refresh remaining-time labels.
    public event Action<int> OnDayChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        //başlangıç tarihi , kayıt sisteminden çekilebilir 
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        currentDay.OnValueChanged += HandleDayChanged;
    }

    public override void OnNetworkDespawn()
    {
        currentDay.OnValueChanged -= HandleDayChanged;

        base.OnNetworkDespawn();
    }

    public override void OnDestroy()
    {
        if (Instance == this) Instance = null;

        base.OnDestroy();
    }

    private void Update()
    {
        if (!IsServer) return;

        dayTimer += Time.deltaTime;

        while (dayTimer >= dayLengthSeconds)
        {
            dayTimer -= dayLengthSeconds;
            currentDay.Value++;
        }
    }

    private void HandleDayChanged(int previous, int current)
    {
        OnDayChanged?.Invoke(current);
    }
}
