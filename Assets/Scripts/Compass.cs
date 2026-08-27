using System;
using UnityEngine;
[ExecuteAlways]
public class Compass : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public Transform stick;
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    { 
        Vector3 forw = Vector3.forward;
        if(transform.parent!=null)
        {
            forw = transform.parent.InverseTransformDirection(forw);
        }
        stick.localRotation = Quaternion.LookRotation(forw);
    }

}
