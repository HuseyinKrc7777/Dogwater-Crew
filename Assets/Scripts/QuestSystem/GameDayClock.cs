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

    [Tooltip("In-game time of day (hours, 0-24) when a session starts. The clock is UTC-based, " +
             "so at longitude 0 this is also the local solar time: 7 = the sun has just risen.")]
    [Range(0f, 24f)][SerializeField] private float startHourUtc = 7f;

    // UTC on purpose: SunCalc converts the DateTime through DateTimeOffset (a Local date would be shifted
    // by the machine's time zone) and GetLocalSiderealTime reads it through ToOADate (no shift at all).
    // A UTC date makes the sun and the star field agree. Set in Awake so startHourUtc can be used.
    private DateTime StartDate = DateTime.UtcNow.Date;

    public DateTime GetCurrentDate()
    {
        DateTime TempDate = StartDate;

        TempDate = TempDate.AddDays(CurrentDay - 1);
        TempDate = TempDate.AddSeconds(dayTimer * (86400f / dayLengthSeconds));
        return TempDate;
    }
    public double GetLocalSiderealTime(double longitude)
    {
        double jd = Instance.GetCurrentDate().ToOADate() + 2415018.5;

        double T = (jd - 2451545.0) / 36525.0;

        double gmst =
            280.46061837
            + 360.98564736629 * (jd - 2451545.0)
            + 0.000387933 * T * T
            - T * T * T / 38710000.0;

        gmst %= 360.0;
        if (gmst < 0)
            gmst += 360.0;

        double lst = gmst + longitude;

        lst %= 360.0;
        if (lst < 0)
            lst += 360.0;

        return lst / 360.0;
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
        StartDate = DateTime.UtcNow.Date.AddHours(startHourUtc);
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
