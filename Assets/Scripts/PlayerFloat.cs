using DogWater;
using Unity.Netcode;
using UnityEngine;

public class PlayerFloat : NetworkBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    FirstPersonController controller;
    private WaterController waterController;
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        controller = GetComponent<FirstPersonController>();
        waterController = GameObject.FindGameObjectWithTag("WaterController").GetComponent<WaterController>();

    }
    // Update is called once per frame
    void Update()
    {
        if(waterController==null)
            waterController = GameObject.FindGameObjectWithTag("WaterController").GetComponent<WaterController>();
        Vector4 steepness = waterController.steepness.Value;
        Vector4 wavelength = waterController.wavelength.Value;
        Vector4 speed = waterController.speed.Value;
        Vector4 directions = waterController.directions.Value;

        Vector3 wave = GerstnerWaveDisplacement.GetWaveDisplacement(
                transform.position,
                new float[] { steepness.x, steepness.y, steepness.z, steepness.w },
                new float[] { wavelength.x, wavelength.y, wavelength.z, wavelength.w },
                new float[] { speed.x, speed.y, speed.z, speed.w },
                new float[] { directions.x, directions.y, directions.z, directions.w }
        );
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
