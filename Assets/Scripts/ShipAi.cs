using Unity.Netcode;
using Unity.Services.Matchmaker.Models;
using Unity.VisualScripting;
using UnityEngine;

//TODO
//gemi yapayzekasının moral ve motivasyonu ile alakalı -
//bu hedef , davranış , kalite gibi değişik şeyler de olabilir temel şeyler de olabilir
//ama bu geminin yapay zekasal herşeyini içerecek şekilde olmalıdır
//- bir struct hazırlanıp
//konfigirasyonun referansı ve paylaşılması olarak kullanılacaktır.
public class ShipAi : NetworkBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    [SerializeField] Ship ship;
    [SerializeField] public LatitudeCoordinate target_latitude = new();
    [SerializeField] public LongitudeCoordinate target_longitude = new();
    [SerializeField] private Vector3 targetPos;
    [SerializeField] private GlobalCoordinate coordinate;
    [SerializeField] private float targetDist;
    [SerializeField] public ShipKind whatKindOfShipIsThis = ShipKind.None;

    void Start()
    {

    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if(!IsServer)
            return;
        float targetDist = coordinate.CalculateDistanceBetweenTwoPoints(coordinate.latitude.Value,coordinate.longitude.Value,target_latitude,target_longitude);
        Vector3 targetDir = coordinate.CalculateDirectionBetweenTwoPoints(coordinate.latitude.Value,coordinate.longitude.Value,target_latitude,target_longitude);
        targetPos = targetDir * targetDist + transform.position;
    }
    // Update is called once per frame
    void Update()
    {
        if(!IsServer)
            return;
        if(whatKindOfShipIsThis != ShipKind.None && whatKindOfShipIsThis != ShipKind.PlayerControlled)
        {
            Vector3 forw = WaterController.Instance.wind.Value.normalized;

            forw = ship.transform.InverseTransformDirection(forw);

            /*
            Vector3 forw = Wind.normalized;
            if(transform.parent!=null)
            {
                forw = transform.parent.InverseTransformDirection(forw);
                if(forw == Vector3.zero)
                {
                    forw = Vector3.down;
                }
            }
            Flag.transform.localRotation = Quaternion.LookRotation(forw);
            */

        
            foreach(Sail sail in ship.sailList)
            {           
                sail.transform.localRotation = Quaternion.LookRotation(forw);
            }
            targetDist = coordinate.CalculateDistanceBetweenTwoPoints(coordinate.latitude.Value,coordinate.longitude.Value,target_latitude,target_longitude);

            if(targetDist < ship.GetComponent<Rigidbody>().linearVelocity.magnitude * 15)
            {
                foreach(Sail sail in ship.sailList)
                {
                    if(sail.openRope.currentValue.Value > 0.1f)
                        sail.openRope.currentValue.Value -=0.1f;
                }
                
            }
            else
            {
                foreach(Sail sail in ship.sailList)
                {
                    if(sail.openRope.currentValue.Value < 0.9f)
                        sail.openRope.currentValue.Value +=0.1f;
                }
            }
            Vector3 targetDir = coordinate.CalculateDirectionBetweenTwoPoints(coordinate.latitude.Value,coordinate.longitude.Value,target_latitude,target_longitude);
            Quaternion targetRot = Quaternion.LookRotation(targetDir);
            transform.localRotation = Quaternion.Lerp(transform.localRotation,targetRot,0.1f);

        }
    }
}
