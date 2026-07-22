using System;
using TMPro;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

public class GlobalCoordinate : NetworkBehaviour
{
    //const int EarthRadius = 6367449;
    //test amaçlı dünya büyüklüğü küçüktür.
    static int EarthRadius = 1500;
    public NetworkVariable<LatitudeCoordinate> latitude = new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<LongitudeCoordinate> longitude = new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public GlobalCoordinate(LatitudeCoordinate latitude , LongitudeCoordinate longitude)
    {
        this.latitude.Value = latitude;
        this.longitude.Value = longitude;
    }
    void Awake()
    {
      
    }
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if(GetComponent<ShipAi>().whatKindOfShipIsThis == ShipKind.PlayerControlled)
        {
            WorldIslandController.Instance.shipCoordinate = this;
            NpcShipController.Instance.shipCoordinate = this;
            if(IsServer)
            {
                WorldIslandController.Instance.CheckIslandsToSpawnOrDespawnThem();
                NpcShipController.Instance.CheckShipsToSpawnOrDespawnThem();
            }
            

        }
    }
    public GlobalCoordinate()
    {
        LatitudeCoordinate la = new()
        {
            Degree = 0,
            Minute = 0,
            Second = 0
        };
        LongitudeCoordinate lo = new()
        {
            Degree = 0,
            Minute = 0,
            Second = 0
        };
        latitude.Value = la;
        longitude.Value = lo;
    }

    public float LongitudeSecondLength(LatitudeCoordinate la)
    {
        float degree = la.GetDegree() * Mathf.Deg2Rad;
        return (float)(Mathf.PI / 180 * EarthRadius * Mathf.Cos(degree) / 3600f);
    }
    public float LatitudeSecondLength(LatitudeCoordinate la)
    {
        float baseRadius = 6367449; 
        float scale = EarthRadius / baseRadius;
        float baseLength = 110574; 
        return baseLength / 3600f * scale;
    }
    public float CalculateDistanceBetweenTwoPoints(LatitudeCoordinate latitude1,LongitudeCoordinate longitude1 ,LatitudeCoordinate latitude2,LongitudeCoordinate longitude2)
    {
        // kaynak : https://stackoverflow.com/questions/27928/calculate-distance-between-two-latitude-longitude-points-haversine-formula
        var dLat = Mathf.Deg2Rad *(latitude2.GetDegree()-latitude1.GetDegree());  // deg2rad below
        var dLon = Mathf.Deg2Rad *(longitude2.GetDegree()-longitude1.GetDegree()); 
        var a = 
            Mathf.Sin(dLat/2) * Mathf.Sin(dLat/2) +
            Mathf.Cos(Mathf.Deg2Rad * (latitude1.GetDegree())) * Mathf.Cos(Mathf.Deg2Rad * (latitude2.GetDegree())) * 
            Mathf.Sin(dLon/2) * Mathf.Sin(dLon/2)
            ; 
        var c = 2 * Mathf.Atan2(Mathf.Sqrt(a), Mathf.Sqrt(1-a)); 
        var d = EarthRadius * c; // Distance in km
        return d;
        
    } 

    public Vector3 CalculateDirectionBetweenTwoPoints(LatitudeCoordinate latitude1,LongitudeCoordinate longitude1 ,LatitudeCoordinate latitude2,LongitudeCoordinate longitude2)
    {

        float latRad = ((latitude1.GetDegree() + latitude2.GetDegree()) * 0.5f) * Mathf.Deg2Rad;

        float x = (longitude2.GetDegree() - longitude1.GetDegree()) * Mathf.Cos(latRad);
        
        float y = latitude2.GetDegree() - latitude1.GetDegree();
        
        return new Vector3(x,0 ,y).normalized;
        
    } 

}