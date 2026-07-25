using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class Ship : NetworkBehaviour
{
    //TODO gemi ve gemi içindeki şeylerin modelleri ayarlandığında root dışında hiçbir obje network object olarak bulunmayacaktır.
    //bu objelerin kontrol scriptleri root objede bulunacak ve bu obejeri referanslar ile kontrol edeceklerdir.
    // kaynak =  https://discussions.unity.com/t/how-to-handle-spawning-nested-network-objects/1634399/2 
    private Vector3 _lastFramePosition;
    public Vector3 VisualDelta { get; private set; }

    // Logic for rotation (if needed for the player's Move function)
    private Quaternion _lastFrameRotation;
    public Quaternion VisualRotationDelta { get; private set; }
    public float VerticalVelocity { get; private set; }

    public Wheel wheel;

    public Anchor anchor;
    public List<Cannon> cannons;
    public List<Sail> sailList;

    public NetworkVariable<float> damage = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<float> waterInsideTheShip = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public GlobalCoordinate coordinate;
    Vector3 lastCoordinateChangePosition = Vector3.zero;
    static public Ship PlayerShip;
    void Start()
    {
        _lastFramePosition = transform.position;
        _lastFrameRotation = transform.rotation;
    }
    void Awake()
    {
        lastCoordinateChangePosition = transform.position;
    }
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        foreach(Sail sail in transform.GetComponentsInChildren<Sail>())
        {
            sailList.Add(sail);
        }
        foreach(Cannon cannon in transform.GetComponentsInChildren<Cannon>())
        {
            cannons.Add(cannon);
        }
        if(GetComponent<ShipAi>().whatKindOfShipIsThis == ShipKind.PlayerControlled)
        {
            PlayerShip = this;
            NpcShipController.Instance.CheckShipsToSpawnOrDespawnThem();
            WorldIslandController.Instance.CheckIslandsToSpawnOrDespawnThem();
        }
    }
    float counter = 0.0f;
    void Update()
    {
        if (Time.deltaTime > 0)
        {
            VerticalVelocity = (transform.position.y - _lastFramePosition.y) / Time.deltaTime;
        }
        // Calculate exactly how much the VISUAL model moved this frame
        Vector3 currentPos = transform.position;
        Quaternion currentRot = transform.rotation;

        VisualDelta = currentPos - _lastFramePosition;
        VisualRotationDelta = currentRot * Quaternion.Inverse(_lastFrameRotation);

        _lastFramePosition = currentPos;
        _lastFrameRotation = currentRot;
        /*
        if(counter>=1.0f)
        {
            counter=0;
            Debug.Log("Yeni pozisyon / düzenli log" + coordinate.latitude.Value  + " " +coordinate.longitude.Value);
        }
        else
            counter+=Time.deltaTime;*/
        if (IsServer && counter >= 1.0f)
        {
            if (damage.Value > 0)
            {
                waterInsideTheShip.Value += Time.deltaTime * damage.Value;
            }
            if(coordinate==null)
                return;
            var la = coordinate.latitude;
            var lo = coordinate.longitude;
            if (Mathf.Abs(lastCoordinateChangePosition.z - transform.position.z) > GlobalCoordinate.LatitudeSecondLength(coordinate.latitude))
            {
                float value = transform.position.z - lastCoordinateChangePosition.z;
                int multiplier = value > 0 ? 1 : -1;
                
                if (Mathf.Abs(value) >= GlobalCoordinate.LatitudeDegreeLength(coordinate.latitude))
                {
                    int d = Mathf.FloorToInt(Mathf.Abs(value) / GlobalCoordinate.LatitudeDegreeLength(coordinate.latitude));
                    la.AddDegree(d * multiplier);
                    value = Mathf.Abs(value) % GlobalCoordinate.LatitudeDegreeLength(coordinate.latitude) * Mathf.Sign(value);
                    lastCoordinateChangePosition.z += GlobalCoordinate.LatitudeDegreeLength(coordinate.latitude) * multiplier *d;
                }
                if (Mathf.Abs(value) >= GlobalCoordinate.LatitudeMinuteLength(coordinate.latitude))
                {
                    int m = Mathf.FloorToInt(Mathf.Abs(value) / GlobalCoordinate.LatitudeMinuteLength(coordinate.latitude));
                    value = Mathf.Abs(value) % GlobalCoordinate.LatitudeMinuteLength(coordinate.latitude) * Mathf.Sign(value);
                    la.AddMinute(m * multiplier);
                    lastCoordinateChangePosition.z += GlobalCoordinate.LatitudeMinuteLength(coordinate.latitude) * multiplier *m;
                }
                if(Mathf.Abs(value) >= GlobalCoordinate.LatitudeSecondLength(coordinate.latitude))
                {
                    int s = Mathf.FloorToInt(Mathf.Abs(value) / GlobalCoordinate.LatitudeSecondLength(coordinate.latitude));
                    value = Mathf.Abs(value) % GlobalCoordinate.LatitudeSecondLength(coordinate.latitude) * Mathf.Sign(value);
                    la.AddSecond(s * multiplier);
                    lastCoordinateChangePosition.z += GlobalCoordinate.LatitudeSecondLength(coordinate.latitude) * multiplier *s;

                }

                if((la.Degree >= 90 || la.Degree<=-90 )&& la.Minute!=0 &&la.Second!=0)
                    la = coordinate.latitude;

            }
            if (Mathf.Abs(lastCoordinateChangePosition.x - transform.position.x) > GlobalCoordinate .LongitudeSecondLength(coordinate.latitude))
            {
                float value = transform.position.x - lastCoordinateChangePosition.x;
                int multiplier = value > 0 ? 1 : -1;

                if (Mathf.Abs(value) >= GlobalCoordinate.LongitudeDegreeLength(coordinate.latitude))
                {
                    int d = Mathf.FloorToInt(Mathf.Abs(value) / GlobalCoordinate.LongitudeDegreeLength(coordinate.latitude));
                    lo.AddDegree(d * multiplier);
                    value = Mathf.Abs(value) % GlobalCoordinate.LongitudeDegreeLength(coordinate.latitude) * Mathf.Sign(value);
                    lastCoordinateChangePosition.x += GlobalCoordinate.LongitudeDegreeLength(coordinate.latitude) * multiplier *d;
                }
                if (Mathf.Abs(value) >= GlobalCoordinate.LongitudeMinuteLength(coordinate.latitude))
                {
                    int m = Mathf.FloorToInt(Mathf.Abs(value) / GlobalCoordinate.LongitudeMinuteLength(coordinate.latitude));
                    value = Mathf.Abs(value) % GlobalCoordinate.LongitudeMinuteLength(coordinate.latitude) * Mathf.Sign(value);
                    lo.AddMinute(m * multiplier);
                    lastCoordinateChangePosition.x += GlobalCoordinate.LongitudeMinuteLength(coordinate.latitude) * multiplier *m;
                }
                if(Mathf.Abs(value) >= GlobalCoordinate.LongitudeSecondLength(coordinate.latitude))
                {
                    int s = Mathf.FloorToInt(Mathf.Abs(value) / GlobalCoordinate.LongitudeSecondLength(coordinate.latitude));
                    value = Mathf.Abs(value) % GlobalCoordinate.LongitudeSecondLength(coordinate.latitude) * Mathf.Sign(value);
                    lo.AddSecond(s * multiplier);
                    lastCoordinateChangePosition.x += GlobalCoordinate.LongitudeSecondLength(coordinate.latitude) * multiplier *s;

                }
            }
            counter = 0;
            coordinate.latitude = la;
            coordinate.longitude = lo;

            Debug.Log("Yeni pozisyon / düzenli log" + coordinate.latitude + " " + coordinate.longitude);

        }
        else
            counter += Time.deltaTime;
    }
    private void OnCollisionEnter(Collision other)
    {
        if (other.gameObject.GetComponent<IDamageDealer>() != null)
        {
            float damageValue = other.gameObject.GetComponent<IDamageDealer>().DamageAmount;
            damage.Value += damageValue;
            //burada hasar verilen yere göre bölgesel tamir edilebilir hasar spawnlanacak
        }
    }
    

}


public enum ShipKind
{
    None,
    PlayerControlled,
    Enemy,
    Neutral,
    Friendly
}