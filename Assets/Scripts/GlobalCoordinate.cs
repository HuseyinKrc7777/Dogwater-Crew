using System;
using TMPro;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

[Serializable]
public class GlobalCoordinate : INetworkSerializable
{
    //const int EarthRadius = 6367449;
    //test amaçlı dünya büyüklüğü küçüktür.
    static int EarthRadius = 1500;
    public LatitudeCoordinate latitude;
    public LongitudeCoordinate longitude;
    public GlobalCoordinate(LatitudeCoordinate latitude , LongitudeCoordinate longitude)
    {
        this.latitude = latitude;
        this.longitude = longitude;
    }
    void Awake()
    {
      
    }
    /*
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
    }*/
    

    public static float LongitudeSecondLength(LatitudeCoordinate la)
    {
        float degree = la.GetDegree() * Mathf.Deg2Rad;
        return (float)(Mathf.PI / 180 * EarthRadius * Mathf.Cos(degree) / 3600f);
    }
    public static float LongitudeMinuteLength(LatitudeCoordinate la)
    {
        float degree = la.GetDegree() * Mathf.Deg2Rad;
        return (float)(Mathf.PI / 180 * EarthRadius * Mathf.Cos(degree) / 60f);
    }
    public static float LongitudeDegreeLength(LatitudeCoordinate la)
    {
        float degree = la.GetDegree() * Mathf.Deg2Rad;
        return (float)(Mathf.PI / 180 * EarthRadius * Mathf.Cos(degree) );
    }
    public static float LatitudeSecondLength(LatitudeCoordinate la)
    {
        float baseRadius = 6367449; 
        float scale = EarthRadius / baseRadius;
        float baseLength = 110574; 
        return baseLength / 3600f * scale;
    }
     public static float LatitudeMinuteLength(LatitudeCoordinate la)
    {
        float baseRadius = 6367449; 
        float scale = EarthRadius / baseRadius;
        float baseLength = 110574; 
        return baseLength / 60 * scale;
    }
     public static float LatitudeDegreeLength(LatitudeCoordinate la)
    {
        float baseRadius = 6367449; 
        float scale = EarthRadius / baseRadius;
        float baseLength = 110574; 
        return baseLength * scale;
    }
    public static float CalculateDistanceBetweenTwoPoints(GlobalCoordinate coordinate1,GlobalCoordinate coordinate2)
    {
        float latitude1 = coordinate1.latitude.GetDegree();
        float longitude1 = coordinate1.longitude.GetDegree();

        float latitude2 = coordinate2.latitude.GetDegree();
        float longitude2 = coordinate2.longitude.GetDegree();
        // kaynak : https://stackoverflow.com/questions/27928/calculate-distance-between-two-latitude-longitude-points-haversine-formula
        var dLat = Mathf.Deg2Rad *(latitude2-latitude1);  // deg2rad below
        var dLon = Mathf.Deg2Rad * Mathf.DeltaAngle(longitude1,longitude2); 
        var a = 
            Mathf.Sin(dLat/2) * Mathf.Sin(dLat/2) +
            Mathf.Cos(Mathf.Deg2Rad * latitude1) * Mathf.Cos(Mathf.Deg2Rad * latitude2) * 
            Mathf.Sin(dLon/2) * Mathf.Sin(dLon/2)
            ; 
        var c = 2 * Mathf.Atan2(Mathf.Sqrt(a), Mathf.Sqrt(1-a)); 
        var d = EarthRadius * c; // Distance in km
        return d;
        
    } 

    public static Vector3 CalculateDirectionBetweenTwoPoints(GlobalCoordinate coordinate1,GlobalCoordinate coordinate2)
    {
        float latitude1 = coordinate1.latitude.GetDegree();
        float longitude1 = coordinate1.longitude.GetDegree();

        float latitude2 = coordinate2.latitude.GetDegree();
        float longitude2 = coordinate2.longitude.GetDegree();

        float latRad = (latitude1 + latitude2) * 0.5f * Mathf.Deg2Rad;

        float x =  Mathf.DeltaAngle(longitude1, longitude2) * Mathf.Cos(latRad);
        
        float y = latitude2 - latitude1;
        
        return new Vector3(x,0 ,y).normalized;
        
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref latitude);
        serializer.SerializeValue(ref longitude);

    }
}