using DogWater;
using SunCalcSharp.Formulas;
using Unity.Netcode;
using UnityEngine;

public class PlayerFloat : NetworkBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    FirstPersonController controller;

    private void Awake()
    {
        controller = GetComponent<FirstPersonController>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
    }

    // Update is called once per frame
    void Update()
    {
        if (!IsSpawned || !IsOwner)
            return;

        // The player prefab is spawned by StartHost() before the gameplay scene is loaded,
        // so the scene's WaterController does not exist yet for the first frames.
        // Unity's == null is required here: WaterController never clears its static Instance,
        // so a previous session can leave a destroyed object behind.
        if (WaterController.Instance == null)
            return;

        Vector3? temp = WaterController.Instance.GetWave(transform.position);
        Vector3 wave = new();
        if(temp!=null)
            wave = (Vector3)temp;
        else
            wave = transform.position;
        if(controller.ship==null && !controller.Grounded && transform.position.y + 1 < wave.y)
        {
            controller._verticalVelocity -= controller.Gravity * Time.deltaTime ;
            if(controller._verticalVelocity < 0)
            {
                controller._verticalVelocity += (wave.y - transform.position.y ) * Time.deltaTime * 3;      
            }
            else
            {
                controller._verticalVelocity += (wave.y - transform.position.y ) * Time.deltaTime / 3;
            }
        }
        
    }

   

    private Vector3 velocity;

}
