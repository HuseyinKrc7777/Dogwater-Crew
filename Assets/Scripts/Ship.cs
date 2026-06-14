using Unity.Netcode;
using UnityEngine;

public class Ship : NetworkBehaviour
{
    private Vector3 _lastFramePosition;
    public Vector3 VisualDelta { get; private set; }

    // Logic for rotation (if needed for the player's Move function)
    private Quaternion _lastFrameRotation;
    public Quaternion VisualRotationDelta { get; private set; }
    public float VerticalVelocity { get; private set; }

    public Wheel wheel;

    public Anchor anchor;


    public NetworkVariable<float> damage = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<float> waterInsideTheShip = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    GlobalCoordinate coordinate;
    Vector3 lastCoordinateChangePosition = Vector3.zero;
    void Start()
    {
        _lastFramePosition = transform.position;
        _lastFrameRotation = transform.rotation;
        coordinate = GetComponent<GlobalCoordinate>();
    }
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

    }
    float counter = 0.0f;
    void Update()
    {
        if(coordinate==null)
            coordinate = new();
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
        if(counter>=1.0f)
        {
            counter=0;
            Debug.Log("Yeni pozisyon / düzenli log" + coordinate.latitude.Value  + " " +coordinate.longtitude.Value);
        }
        else
            counter+=Time.deltaTime;
        if(IsServer)
        {
            if(damage.Value > 0)
            {
                waterInsideTheShip.Value += Time.deltaTime * damage.Value;
            }
            float latitudeChangeValue = coordinate.LatitudeSecondLength(coordinate.latitude.Value);
            float longtitudeChangeValue = coordinate.LatitudeSecondLength(coordinate.latitude.Value);
            while(Mathf.Abs(lastCoordinateChangePosition.z - transform.position.z) > latitudeChangeValue)
            {
                if(transform.position.z - lastCoordinateChangePosition.z > 0)
                {
                    Coordinate data = coordinate.latitude.Value;
                    if(data.Direction == GlobalDirections.North)
                        data.Second++;
                    else
                        data.Second--;
                    coordinate.ChangePosition(data,coordinate.longtitude.Value);
                    lastCoordinateChangePosition.z += latitudeChangeValue;
                }
                else
                {
                    Coordinate data = coordinate.latitude.Value;
                    if(data.Direction == GlobalDirections.North)
                        data.Second--;
                    else
                        data.Second++;
                    coordinate.ChangePosition(data,coordinate.longtitude.Value);
                    lastCoordinateChangePosition.z -= latitudeChangeValue;
                }
        
            }
            while(Mathf.Abs(lastCoordinateChangePosition.x - transform.position.x) > longtitudeChangeValue)
            {
                if(transform.position.x - lastCoordinateChangePosition.x > 0)
                {
                    Coordinate data = coordinate.longtitude.Value;
                    if(data.Direction == GlobalDirections.West)
                        data.Second++;
                    else
                        data.Second--;
                    coordinate.ChangePosition(coordinate.latitude.Value,data);
                    lastCoordinateChangePosition.x += longtitudeChangeValue;
                }
                else
                {
                    
                    Coordinate data = coordinate.longtitude.Value;
                    if(data.Direction==GlobalDirections.West)
                        data.Second--;
                    else
                        data.Second++;
                    coordinate.ChangePosition(coordinate.latitude.Value,data);
                    lastCoordinateChangePosition.x -= longtitudeChangeValue;
                }
        
            }
        }

       
    }

}