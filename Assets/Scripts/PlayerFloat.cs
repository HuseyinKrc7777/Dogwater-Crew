using DogWater;
using Unity.Netcode;
using UnityEngine;

public class PlayerFloat : NetworkBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    FirstPersonController controller;
    private WaterController waterController;

    private void Awake()
    {
        controller = GetComponent<FirstPersonController>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        TryCacheWaterController();
    }

    // Update is called once per frame
    void Update()
    {
        if (!IsSpawned || !IsOwner)
            return;

        if (waterController == null && !TryCacheWaterController())
            return;

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

    private bool TryCacheWaterController()
    {
        GameObject waterControllerObject = GameObject.FindGameObjectWithTag("WaterController");
        return waterControllerObject != null
            && waterControllerObject.TryGetComponent(out waterController);
    }

    private Vector3 velocity;

}
