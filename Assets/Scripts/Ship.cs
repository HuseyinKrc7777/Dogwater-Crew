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
        if (coordinate == null)
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
            var la = coordinate.latitude.Value;
            var lo = coordinate.longitude.Value;
            while (Mathf.Abs(lastCoordinateChangePosition.z - transform.position.z) > coordinate.LatitudeSecondLength(la))
            {
                float value = transform.position.z - lastCoordinateChangePosition.z;
                int multiplier = value > 0 ? 1 : -1;

                if (value >= coordinate.LatitudeSecondLength(la) * 3600)
                {
                    la.AddDegree(1 * multiplier);
                    lastCoordinateChangePosition.z += coordinate.LatitudeSecondLength(la) * 3600 * multiplier;
                }
                else if (value >= coordinate.LatitudeSecondLength(la) * 60)
                {
                    la.AddMinute(1 * multiplier);
                    lastCoordinateChangePosition.z += coordinate.LatitudeSecondLength(la) * 60 * multiplier;
                }
                else
                {
                    la.AddSecond(1 * multiplier);
                    lastCoordinateChangePosition.z += coordinate.LatitudeSecondLength(la) * multiplier;
                }

            }
            while (Mathf.Abs(lastCoordinateChangePosition.x - transform.position.x) > coordinate.LongitudeSecondLength(la))
            {
                float value = transform.position.x - lastCoordinateChangePosition.x;
                int multiplier = value > 0 ? 1 : -1;

                if (value >= coordinate.LatitudeSecondLength(la) * 3600)
                {
                    lo.AddDegree(1 * multiplier);
                    lastCoordinateChangePosition.x += coordinate.LatitudeSecondLength(la) * 3600 * multiplier;
                }
                else if (value >= coordinate.LatitudeSecondLength(la) * 60)
                {
                    lo.AddMinute(1 * multiplier);
                    lastCoordinateChangePosition.x += coordinate.LatitudeSecondLength(la) * 60 * multiplier;
                }
                else
                {
                    lo.AddSecond(1 * multiplier);
                    lastCoordinateChangePosition.x += coordinate.LatitudeSecondLength(la) * multiplier;
                }
            }
            counter = 0;
            coordinate.latitude.Value = la;
            coordinate.longitude.Value = lo;

            Debug.Log("Yeni pozisyon / düzenli log" + coordinate.latitude.Value + " " + coordinate.longitude.Value);

        }
        else
            counter += Time.deltaTime;


    }

}