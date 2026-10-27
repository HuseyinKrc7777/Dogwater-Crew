using System;
using Unity.Netcode;
using UnityEngine;
using SunCalcSharp;
using Unity.Mathematics;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
// shader kaynak = https://kelvinvanhoorn.com/tutorials/unity_skybox_shader/#star-rotations
// hesaplamalar kaynak = https://github.com/webbwebbwebb/suncalcsharp
public class SkyboxController : NetworkBehaviour
{
    public struct SkyData : INetworkSerializable
    {
        public Quaternion TargetSunRotation;
        public Quaternion StartSunRotation;
        public Quaternion TargetMoonRotation;
        public Quaternion StartMoonRotation;
        public Vector3 StartSkyRotation;
        public Vector3 TargetSkyRotation;
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref TargetSunRotation);
            serializer.SerializeValue(ref StartSunRotation);
            serializer.SerializeValue(ref TargetMoonRotation);
            serializer.SerializeValue(ref StartMoonRotation);
            serializer.SerializeValue(ref StartSkyRotation);
            serializer.SerializeValue(ref TargetSkyRotation);
        }
    }

    [SerializeField] Transform _Sun;
    [SerializeField] Transform _Moon;
    [SerializeField] public Volume _Volume;

    public PhysicallyBasedSky _Sky;

    NetworkVariable<SkyData> _SkyData = new();


    //NetworkVariable<float> PlayerShipLongitude = new();

    public static SkyboxController Instance { get; private set; }
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }
    float counter = 0.0f;


    void Start()
    {
        // .profile, not .sharedProfile: sharedProfile is the project asset, so writing spaceRotation into it
        // every frame changed the asset on disk. .profile gives this Volume its own runtime copy.
        VolumeProfile profile = _Volume.profile;
        if (profile.TryGet<PhysicallyBasedSky>(out var PhysicallyBasedSky))
        {
            _Sky = PhysicallyBasedSky;
        }

    }

    public override void OnDestroy()
    {
        // Volume never destroys the copy its .profile getter created, so free it (and its cloned components).
        if (_Volume != null && _Volume.HasInstantiatedProfile())
        {
            VolumeProfile runtimeProfile = _Volume.profile;
            foreach (VolumeComponent component in runtimeProfile.components)
            {
                if (component != null) Destroy(component);
            }
            Destroy(runtimeProfile);
        }

        base.OnDestroy();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        _SkyData.OnValueChanged += OnDataChanged;

        // The ship (and Start(), which finds _Sky) may not be ready yet when this object spawns - coming in
        // through Bootstrap the ship spawns after us. Then LateUpdate keeps retrying until it is.
        if (IsServer)
        {
            TryPublishFirstData();
        }
    }

    public override void OnNetworkDespawn()
    {
        _SkyData.OnValueChanged -= OnDataChanged;
        base.OnNetworkDespawn();
    }

    // A default SkyData holds all-zero quaternions: nothing has been computed yet.
    private static bool HasData(in SkyData data)
    {
        Quaternion q = data.TargetSunRotation;
        return q.x != 0f || q.y != 0f || q.z != 0f || q.w != 0f;
    }

    private bool CanCalculate()
    {
        return Ship.PlayerShip != null && Ship.PlayerShip.coordinate != null
            && GameDayClock.Instance != null && _Sky != null;
    }

    // Server: the first valid data is published with Start == Target, so every peer SNAPS the sun to its
    // real position instead of sweeping there from whatever pose the scene was saved with.
    private bool TryPublishFirstData()
    {
        if (!CanCalculate()) return false;

        SkyData newData = CalculateData();
        newData.StartSunRotation = newData.TargetSunRotation;
        newData.StartMoonRotation = newData.TargetMoonRotation;
        newData.StartSkyRotation = newData.TargetSkyRotation;
        _SkyData.Value = newData;
        counter = 0;
        UseData();
        return true;
    }

    private void OnDataChanged(SkyData previousValue, SkyData newValue)
    {
        counter = 0;
    }
    Vector3 AzAltToVector(float azimuthRad, float altitudeRad)
    {
        float horiz = Mathf.Cos(altitudeRad);
        return new Vector3(
            -Mathf.Sin(azimuthRad) * horiz,
             Mathf.Sin(altitudeRad),
            -Mathf.Cos(azimuthRad) * horiz
        );
    }
    private SkyData CalculateData()
    {
        SkyData newData = new();

        Ship ship = Ship.PlayerShip.GetComponent<Ship>();
        float latitude = ship.coordinate.latitude.GetDegree();
        float longitude = ship.coordinate.longitude.GetDegree();

        DateTime date = GameDayClock.Instance.GetCurrentDate();
        SunPosition sunPos = SunCalc.GetPosition(date, latitude, longitude);
        MoonPosition moonPos = MoonCalc.GetMoonPosition(date, latitude, longitude);

        Vector3 sunDir = AzAltToVector((float)sunPos.Azimuth, (float)sunPos.Altitude);
        Vector3 moonDir = AzAltToVector((float)moonPos.Azimuth, (float)moonPos.Altitude);

        // sunDir/moonDir point FROM the observer TO the body. A directional light shines along its forward,
        // so the light must face the opposite way; LookRotation(sunDir) lit the scene from the mirrored
        // point of the sky (a sun 60 degrees below the horizon at night showed up 60 degrees above it).
        Quaternion sunRotation = Quaternion.LookRotation(-sunDir, Vector3.up);
        Quaternion moonRotation = Quaternion.LookRotation(-moonDir, Vector3.up);
        Vector3 skyRotation = GetSkyRotation(latitude, (float)GameDayClock.Instance.GetLocalSiderealTime(longitude));

        newData.StartSunRotation = _Sun.rotation;
        newData.TargetSunRotation = sunRotation;

        newData.StartMoonRotation = _Moon.rotation;
        newData.TargetMoonRotation = moonRotation;

        newData.StartSkyRotation = _Sky.spaceRotation.value;
        newData.TargetSkyRotation = skyRotation;
        


        return newData;
    }
    Vector3 GetSkyRotation(float latitude, float localSiderealTime)
    {
        float tilt = (latitude - 90f) * Mathf.Deg2Rad;
        float spin = (0.75f - localSiderealTime) * Mathf.PI * 2f;

        Quaternion tiltRotation = Quaternion.AngleAxis(
            tilt * Mathf.Rad2Deg,
            Vector3.right
        );

        Quaternion spinRotation = Quaternion.AngleAxis(
            spin * Mathf.Rad2Deg,
            Vector3.up
        );

        Quaternion rotation = spinRotation * tiltRotation;

        return rotation.eulerAngles;
    }
    private void UseData()
    {
        float t = Mathf.Clamp01(counter / 1);

        _Moon.rotation = Quaternion.Slerp(
        _SkyData.Value.StartMoonRotation,
        _SkyData.Value.TargetMoonRotation,
        t
        );

        _Sun.rotation = Quaternion.Slerp(
        _SkyData.Value.StartSunRotation,
        _SkyData.Value.TargetSunRotation,
        t
        );

        _Sky.spaceRotation.value = Vector3.Slerp(
        _SkyData.Value.StartSkyRotation,
        _SkyData.Value.TargetSkyRotation,
        t
        );


    }

    void LateUpdate()
    {
        if (!IsSpawned) return;

        // Nothing valid yet: leave the sun, moon and stars alone (UseData would slerp between zero
        // quaternions). The server keeps trying to publish the first data.
        if (!HasData(_SkyData.Value))
        {
            if (IsServer) TryPublishFirstData();
            return;
        }

        UseData();

        if (!IsServer)
        {
            counter += Time.deltaTime;
            return;
        }

        if (counter < 1.0f)
        {

            counter += Time.deltaTime;
        }
        else
        {
            //TODO su shader'ındaki ufuk rengi ve doldurucu su shader'ındaki ufuk rengi de günün durumuna göre güncellenecek 
            //yıldız cubemap ' i git'de sıkıntı çıkmasın diye düşük kaliteye geçildi,
            //https://svs.gsfc.nasa.gov/4851 buradan yüksek kalitesi indirilik kullanılabilir.
            if (CanCalculate())
            {
                SkyData newData = CalculateData();
                _SkyData.Value = newData;
            }
            counter = 0;
        }


    }


}

