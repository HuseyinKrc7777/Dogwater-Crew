using System;
using System.Runtime.CompilerServices;
using Blocks.Sessions.Common;
using Mono.Cecil.Cil;
using Unity.Netcode;
using UnityEngine;

public class SunController : NetworkBehaviour
{

    [SerializeField] Transform Sun;
    NetworkVariable<Quaternion> TargetSunRotation = new();
    NetworkVariable<Quaternion> StartSunRotation = new();

    public static SunController Instance { get; private set; }
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }
    void Start()
    {

    }

    // Update is called once per frame
    float counter = 0.0f;


    void Update()
    {
        float t = Mathf.Clamp01(counter / 1);
        Sun.rotation = Quaternion.Slerp(
        StartSunRotation.Value,
        TargetSunRotation.Value,
        t
        );
        
        if (!IsServer)
            return;
        if (counter < 1.0f)
        {
            counter += Time.deltaTime;
            return;
        }
        else
        {
            counter = 0;
        }

        Ship ship = Ship.PlayerShip.GetComponent<Ship>();
        //oyun içi tarih , 1 ocaktan başlayabilir ? ya da rastgele bir günden başlayabilir mi acaba ??
        DateTime currentDate = DateTime.UtcNow;
        currentDate = currentDate.AddSeconds(GameDayClock.Instance.dayTimer * (86400f / GameDayClock.Instance.dayLengthSeconds));
        float azimuth = SolarCalculator.GetAzimuth(currentDate, ship.coordinate.latitude.GetDegree(), ship.coordinate.longitude.GetDegree());
        float elevation = SolarCalculator.GetElevation(currentDate, ship.coordinate.latitude.GetDegree(), ship.coordinate.longitude.GetDegree());

        Vector3 sunDirection = new Vector3(
            Mathf.Sin(azimuth * Mathf.Deg2Rad) * Mathf.Cos(elevation * Mathf.Deg2Rad),
            Mathf.Sin(elevation * Mathf.Deg2Rad),
            Mathf.Cos(azimuth * Mathf.Deg2Rad) * Mathf.Cos(elevation * Mathf.Deg2Rad)
        );
        StartSunRotation.Value = Sun.rotation;
        TargetSunRotation.Value = Quaternion.LookRotation(-sunDirection);

        Debug.LogError(-sunDirection);
    }


}
public static class SolarCalculator
{
    /*
    referans : 
    https://www.benjaminjohnston.com.au/downloads/solar/index.html
    https://www.benjaminjohnston.com.au/downloads/solar/solar.js
    LLM yardımı ile uyarlanmıştır.
    */
    // -----------------------------
    // Constants
    // -----------------------------

    const float SiderealRotation = 86164.09054f;
    const float SiderealOrbit = 365.242190f;
    const float AxialTilt = 23.44f;

    const float SolsticeLongitude = 107.2f;

    const float OrbitEccentricity = 0.01671022f;

    static readonly DateTime SummerSolstice =
        new DateTime(2015, 12, 22, 4, 49, 0, DateTimeKind.Utc);

    static readonly DateTime Perihelion =
        new DateTime(2016, 1, 2, 22, 49, 0, DateTimeKind.Utc);

    static readonly float SolsticeToPerihelion =
        (float)(Perihelion - SummerSolstice).TotalSeconds;

    static readonly Vector3 Up = Vector3.right;     // (1,0,0)
    static readonly Vector3 North = Vector3.forward;// (0,0,1)
    static readonly Vector3 East = Vector3.up;      // (0,1,0)

    // ---------------------------------------------------
    // Kepler
    // ---------------------------------------------------

    static float E(float e, float M)
    {
        float x = M;

        for (int i = 0; i < 2; i++)
        {
            float f = x - e * Mathf.Sin(x) - M;
            float df = 1f - e * Mathf.Cos(x);
            x -= f / df;
        }

        return x;
    }

    static float Phi(float e, float E)
    {
        return 2f * Mathf.Atan2(
            Mathf.Sqrt(1 + e) * Mathf.Sin(E * 0.5f),
            Mathf.Sqrt(1 - e) * Mathf.Cos(E * 0.5f));
    }

    // ---------------------------------------------------
    // Earth Rotation
    // ---------------------------------------------------

    static Quaternion RotateEarth(float currentTime, float latitude, float longitude)
    {
        Quaternion rot = Quaternion.identity;

        rot *= Quaternion.AngleAxis(-latitude, Vector3.up);
        rot *= Quaternion.AngleAxis(longitude, Vector3.forward);
        rot *= Quaternion.AngleAxis(-SolsticeLongitude, Vector3.forward);
        rot *= Quaternion.AngleAxis(
            currentTime / SiderealRotation * 360f,
            Vector3.forward);
        rot *= Quaternion.AngleAxis(-AxialTilt, Vector3.up);

        return rot;
    }

    // ---------------------------------------------------
    // Sun Vector
    // ---------------------------------------------------

    static Vector3 SunVector(float currentTime, bool useEccentricity)
    {
        float sunAngle =
            currentTime /
            86400f /
            SiderealOrbit *
            Mathf.PI * 2f;

        if (useEccentricity)
        {
            float meanAnomaly =
                (currentTime - SolsticeToPerihelion) /
                86400f /
                SiderealOrbit *
                Mathf.PI * 2f;

            float solsticeMeanAnomaly =
                -SolsticeToPerihelion /
                86400f /
                SiderealOrbit *
                Mathf.PI * 2f;

            float solsticeAngle =
                Phi(OrbitEccentricity,
                    E(OrbitEccentricity, solsticeMeanAnomaly));

            float trueAnomaly =
                Phi(OrbitEccentricity,
                    E(OrbitEccentricity, meanAnomaly));

            sunAngle = trueAnomaly - solsticeAngle;
        }

        Vector3 earth =
            Quaternion.AngleAxis(sunAngle * Mathf.Rad2Deg, Vector3.forward)
            * Vector3.left;

        return -earth;
    }

    // ---------------------------------------------------
    // Public API
    // ---------------------------------------------------

    public static float GetElevation(DateTime utc, float latitude, float longitude)
    {
        float time =
            (float)(utc.ToUniversalTime() - SummerSolstice).TotalSeconds;

        Vector3 sun = SunVector(time, true);

        Quaternion earth = RotateEarth(time, latitude, longitude);

        Vector3 up = earth * Up;

        float angle = Mathf.Acos(Vector3.Dot(sun.normalized, up.normalized));

        return 90f - angle * Mathf.Rad2Deg;
    }

    public static float GetAzimuth(DateTime utc, float latitude, float longitude)
    {
        float time =
            (float)(utc.ToUniversalTime() - SummerSolstice).TotalSeconds;

        Vector3 sun = SunVector(time, true);

        Quaternion earth = RotateEarth(time, latitude, longitude);

        Vector3 north = earth * North;
        Vector3 east = earth * East;

        float dotN = Vector3.Dot(sun, north);
        float dotE = Vector3.Dot(sun, east);

        return Mathf.Atan2(dotE, dotN) * Mathf.Rad2Deg;
    }
}