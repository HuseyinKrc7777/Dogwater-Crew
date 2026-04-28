using DogWater;
using UnityEngine;

public class Player : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public FirstPersonController controller;
    void Start()
    {
        controller = GetComponent<FirstPersonController>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
