using System;
using Unity.Netcode;
using UnityEngine;
using SunCalcSharp;
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
        public float StartLatitude;
        public float TargetLatitude;
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref TargetSunRotation);
            serializer.SerializeValue(ref StartSunRotation);
            serializer.SerializeValue(ref TargetMoonRotation);
            serializer.SerializeValue(ref StartMoonRotation);
            serializer.SerializeValue(ref StartLatitude);
            serializer.SerializeValue(ref TargetLatitude);
        }
    }

    [SerializeField] Transform _Sun;
    [SerializeField] Transform _Moon;
    private float _ShipLat;
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

    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        _SkyData.OnValueChanged += OnDataChanged;

        if (IsServer)
        {
            SkyData newData = CalculateData();
            newData.StartSunRotation = newData.TargetSunRotation; 
            newData.StartMoonRotation = newData.TargetMoonRotation;
            newData.StartLatitude = newData.TargetLatitude;
            _SkyData.Value = newData;
            UseData();
        }


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

        Quaternion sunRotation = Quaternion.LookRotation(sunDir, Vector3.up);
        Quaternion moonRotation = Quaternion.LookRotation(moonDir, Vector3.up);
        
        newData.StartSunRotation = _Sun.rotation;
        newData.TargetSunRotation = sunRotation;

        newData.StartMoonRotation = _Moon.rotation;
        newData.TargetMoonRotation = moonRotation;

        newData.StartLatitude = _ShipLat;
        newData.TargetLatitude = latitude; 

        return newData;
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


        _ShipLat = Mathf.Lerp(
        _SkyData.Value.StartLatitude,
        _SkyData.Value.TargetLatitude,
        t
        );

        Shader.SetGlobalVector("_SunDir", -_Sun.transform.forward);
        Shader.SetGlobalVector("_MoonDir", -_Moon.transform.forward);
        Shader.SetGlobalMatrix("_MoonSpaceMatrix", new Matrix4x4(-_Moon.transform.forward, _Moon.transform.up, -_Moon.transform.right, Vector4.zero).transpose);

        Shader.SetGlobalFloat("_StarLatitude", _ShipLat);

    }
    void LateUpdate()
    {
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
            SkyData newData = CalculateData();
            _SkyData.Value = newData;
            counter = 0;
        }

        
    }


}

