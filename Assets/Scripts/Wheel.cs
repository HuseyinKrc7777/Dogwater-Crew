using Unity.Netcode;
using UnityEngine;

public interface IInteractable
{
    abstract public void OnInteract();
}
public class Wheel : NetworkBehaviour , IInteractable
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public float rudderRotation = 0.0f;

    public void OnInteract()
    {
        throw new System.NotImplementedException();
    }

    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
