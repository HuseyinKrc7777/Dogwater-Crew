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


    void Start()
    {
        _lastFramePosition = transform.position;
        _lastFrameRotation = transform.rotation;
    }

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
        
        if(IsServer)
        {
             if(damage.Value > 0)
            {
                waterInsideTheShip.Value += Time.deltaTime * damage.Value;
            }
        }
       
    }

}