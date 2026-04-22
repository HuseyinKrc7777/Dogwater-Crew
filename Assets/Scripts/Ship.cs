using Unity.Netcode;
using UnityEngine;

public class Ship : NetworkBehaviour
{
    public Vector3 lastPosition;
    public Vector3 velocityDelta;
    public Quaternion deltaRot;
    public Quaternion shipLastRot;

    void LateUpdate()
    {
        velocityDelta = transform.position - lastPosition;
        lastPosition = transform.position;
        deltaRot = transform.rotation * Quaternion.Inverse(shipLastRot);
        shipLastRot = transform.rotation;
    }

    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
