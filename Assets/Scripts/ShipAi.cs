using System;
using System.Collections.Generic;
using Unity.Multiplayer.Tools.NetStatsMonitor;
using Unity.Netcode;
using Unity.Services.Matchmaker.Models;
using Unity.VisualScripting;
using UnityEngine;
using WebSocketSharp;

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
    [SerializeField] public GlobalCoordinate targetCoordinate;
    [SerializeField] private Vector3 targetPos;
    [SerializeField] private GlobalCoordinate coordinate;
    [SerializeField] private float targetDist;
    [SerializeField] public ShipKind whatKindOfShipIsThis = ShipKind.None;
    [SerializeField] public ShipState shipState = ShipState.Travel;
    [SerializeField] public Ship Target;
    public List<GlobalCoordinate> TravelPositions;
    [SerializeField] private int AttackDistance = 150;
    [SerializeField] private int DetectionkDistance = 500;

    public int TravelPositionCounter = 0;
    public enum ShipState
    {
        Travel,
        Pursuit,
        Attack
    }
    void Start()
    {

    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (!IsServer)
            return;
        float targetDist = GlobalCoordinate.CalculateDistanceBetweenTwoPoints(coordinate,targetCoordinate);
        Vector3 targetDir = GlobalCoordinate.CalculateDirectionBetweenTwoPoints(coordinate,targetCoordinate);
        targetPos = targetDir * targetDist + transform.position;
    }
    private void ControlSails()
    {
        Vector3 forw = WaterController.Instance.wind.Value.normalized;

        forw = ship.transform.InverseTransformDirection(forw);

        foreach (Sail sail in ship.sailList)
        {
            sail.transform.localRotation = Quaternion.LookRotation(forw);
        }
    }

    private void ControlShipRotation()
    {
        float multiplier = GetComponent<Rigidbody>().linearVelocity.magnitude;
        if (multiplier < 0.1f)
            multiplier = 0;
        float RotationSpeed = 0.01f;
        if (shipState == ShipState.Travel || shipState == ShipState.Pursuit)
        {
            //TODO burada rüzgara göre , hedefe gidilemiyorsa başka bir yol denenecek şekilde ayarlananbilir.
            Vector3 targetDir = GlobalCoordinate.CalculateDirectionBetweenTwoPoints(coordinate, targetCoordinate);
            Quaternion targetRot = Quaternion.LookRotation(targetDir);
            transform.localRotation = Quaternion.Lerp(transform.localRotation, targetRot, RotationSpeed * multiplier);
        }
        else if (shipState == ShipState.Attack)
        {
            Vector3 targetDir = Target.transform.position - transform.position;
            targetDir.y = 0;

            Quaternion targetRotation = Quaternion.LookRotation(targetDir);

            Quaternion rotPlus90 = targetRotation * Quaternion.Euler(0, 90, 0);
            Quaternion rotMinus90 = targetRotation * Quaternion.Euler(0, -90, 0);

            float anglePlus90 = Quaternion.Angle(transform.localRotation, rotPlus90);
            float angleMinus90 = Quaternion.Angle(transform.localRotation, rotMinus90);

            Quaternion targetRot = anglePlus90 < angleMinus90 ? rotPlus90 : rotMinus90;

            transform.localRotation = Quaternion.Lerp(
                transform.localRotation,
                targetRot,
                RotationSpeed * multiplier);
        }

    }
    float counter = 0;
    private void FireCannons()
    {
        
        if(shipState != ShipState.Attack)
            return;
        foreach (Cannon cannon in ship.cannons)
        {
            Vector3 target = Target.transform.position;
            Vector3 direction = target - cannon.transform.position;
            float distance = direction.magnitude;

            float angle = CalculateCannonAngle(distance, cannon.fireForce);
            float angleRad = angle * Mathf.Deg2Rad;

            float horizontalVelocity = cannon.fireForce * Mathf.Cos(angleRad);

            float time = distance / horizontalVelocity;
            /* yukarı kısım zaman için hesaplama , aşağı kısım zamanla birlikte hedefin yer değişimini katarak hesaplama */
            Vector3 targetAdjusted = Target.transform.position + Target.GetComponent<Rigidbody>().linearVelocity * time;
            Vector3 directionAdjusted = targetAdjusted - cannon.transform.position;
            float distanceAdjusted = directionAdjusted.magnitude;

            float angleAdjusted = CalculateCannonAngle(distanceAdjusted, cannon.fireForce);
            cannon.cannonPitch.Value = angleAdjusted * -1;

            float dot = Vector3.Dot(direction.normalized, cannon.spawnPoint.forward);
            // nekadar kaliteli ateş edebileceği buradan dot ' Un kontrolü ile yapılıyor.
            if (dot >= 0.85f && angle > 0)
            {
                cannon.OnButtonInput();
            }
        }
    }
    private float CalculateCannonAngle(float distance, float velocity)
    {
        float angle;
        float gravity = Physics.gravity.y * -1;
        float value = (distance * gravity) / (velocity * velocity);

        if (value > 1f)
        {
            angle = 0f;
        }
        else
            angle = 0.5f * Mathf.Asin(value) * Mathf.Rad2Deg;

        return angle;
    }
    void ChangeTravelPosition()
    {
        targetCoordinate = TravelPositions[TravelPositionCounter];
        
        if (TravelPositionCounter < TravelPositions.Count)
            TravelPositionCounter++;
        else
            TravelPositionCounter = 0;
    }
    void TravelControl()
    {
        if (shipState != ShipState.Travel)
            return;

        targetDist = GlobalCoordinate.CalculateDistanceBetweenTwoPoints(coordinate,targetCoordinate);
        /*ship.GetComponent<Rigidbody>().linearVelocity.magnitude * 15*/
        if (targetDist < 5)
        {
            ChangeTravelPosition();
        }

    }
    void PursuitControl()
    {
        if (Target != null && shipState == ShipState.Pursuit)
        {
            targetCoordinate = Target.coordinate;
        }
    }
    void ChangeState()
    {
        //TODOburaya oyuncu gemisinin tespiti yapılacak
        if(Target == null && shipState == ShipState.Travel && whatKindOfShipIsThis==ShipKind.Enemy)
        {
            //TODOBurada npc ' ler arası savaş eklendiğinde değiştirilecek,
            var ps = Ship.PlayerShip;
            if (Vector3.Distance(ps.transform.position, transform.position) < DetectionkDistance)
            {
                Target = ps;
            }
        }
        if (Target != null && shipState == ShipState.Travel && Vector3.Distance(Target.transform.position, transform.position) < DetectionkDistance)
        {
            shipState = ShipState.Pursuit;
        }
        if (shipState == ShipState.Pursuit && Vector3.Distance(Target.transform.position, transform.position) < AttackDistance)
        {
            shipState = ShipState.Attack;
        }
        else if (shipState == ShipState.Attack && Vector3.Distance(Target.transform.position, transform.position) > AttackDistance)
        {
            shipState = ShipState.Pursuit;
        }
        else if (shipState == ShipState.Pursuit && Vector3.Distance(Target.transform.position, transform.position) > DetectionkDistance)
        {
            Target = null;
            shipState = ShipState.Travel;
        }
    }
    void Update()
    {
        if (!IsServer)
            return;
        if (whatKindOfShipIsThis == ShipKind.None || whatKindOfShipIsThis == ShipKind.PlayerControlled)
            return;

        //TODO Yelken kontrolü her kare değil de birkaç saniyede bir çalıştırılabilir.
        if (counter < 2f)
            counter += Time.deltaTime;
        else
        {
            counter = 0;

            ChangeState();

            FireCannons();
            TravelControl();
            PursuitControl();

            ControlSails();
        }
        ControlShipRotation();



    }
}
