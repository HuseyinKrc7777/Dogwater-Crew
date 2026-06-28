using System;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

public class GlobalCoordinate : NetworkBehaviour
{
    //const int EarthRadius = 6367449;
    //test amaçlı dünya büyüklüğü küçüktür.
    //TODO kordinat şeysi değiştirilecek
    static int EarthRadius = 1500;
    public NetworkVariable<Coordinate> latitude = new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<Coordinate> longitude = new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public Coordinate SwitchDirection(Coordinate coordinate)
    {
        if(coordinate.Direction == GlobalDirections.North || coordinate.Direction == GlobalDirections.East)
        {
            coordinate.Direction++;
        }
        else
            coordinate.Direction--;
        
        if(coordinate.Degree < 0)
            coordinate.Degree*=-1;


        if(coordinate.Minute < 0)
            coordinate.Minute*=-1;
   

        if(coordinate.Second < 0)
            coordinate.Second*=-1;
        
        return coordinate;
    }
    public GlobalCoordinate(Coordinate latitude , Coordinate longitude)
    {
        this.latitude.Value = latitude;
        this.longitude.Value = longitude;
    }

    public GlobalCoordinate()
    {
        Coordinate la = new()
        {
            Direction = GlobalDirections.North,
            Degree = 0,
            Minute = 0,
            Second = 0
        };
        Coordinate lo = new()
        {
            Direction = GlobalDirections.West,
            Degree = 0,
            Minute = 0,
            Second = 0
        };
        latitude.Value = la;
        longitude.Value = lo;
    }

    public float LongitudeSecondLength(Coordinate northSouth)
    {
        float degree = northSouth.Degree + northSouth.Minute / 60f + northSouth.Second / 3600f;
        degree *= Mathf.Deg2Rad ;
        return (float)(Mathf.PI / 180 * EarthRadius * Mathf.Cos(degree) / 3600f);
    }
    public float LatitudeSecondLength(Coordinate northSouth)
    {
        float degree = northSouth.Degree + northSouth.Minute / 60f + northSouth.Second / 3600f;
        float phi = degree * Mathf.Deg2Rad;
        float baseRadius = 6367449; 
        float scale = EarthRadius / baseRadius;

        return (111132.95255f
            - 559.84957f * Mathf.Cos(2f * phi)
            + 1.17514f * Mathf.Cos(4f * phi)
            - 0.00230f * Mathf.Cos(6f * phi) ) / 3600f * scale;
    }
    public void ChangePosition(Coordinate newLatitude , Coordinate newLongitude)
    {
        
        Coordinate NorthSouthData = newLatitude;
        Coordinate EastWestData = newLongitude;

        int laVal = 3600 * NorthSouthData.Degree + 60 * NorthSouthData.Minute + NorthSouthData.Second; 
        int loVal = 3600 * EastWestData.Degree + 60 * EastWestData.Minute + EastWestData.Second; 
        
        if(laVal < 0)
            NorthSouthData = SwitchDirection(NorthSouthData);
        if(loVal < 0)
            EastWestData = SwitchDirection(EastWestData);




        while(NorthSouthData.Second >=60)
        {
            NorthSouthData.Minute++;
            NorthSouthData.Second -= 60;
        }
        while(NorthSouthData.Minute >=60)
        {
            NorthSouthData.Degree++;
            NorthSouthData.Minute -= 60;
        }


        while(EastWestData.Second >=60)
        {
            EastWestData.Minute++;
            EastWestData.Second -= 60;
        }
        while(EastWestData.Minute >=60)
        {
            EastWestData.Degree++;
            EastWestData.Minute -= 60;
        }





        while(EastWestData.Second<0)
        {
            EastWestData.Minute--;
            EastWestData.Second+=60;
        }
        while(EastWestData.Minute<0)
        {
            EastWestData.Degree--;
            EastWestData.Minute+=60;
        }


        while(NorthSouthData.Second<0)
        {
            NorthSouthData.Minute--;
            NorthSouthData.Second+=60;
        }
        while(NorthSouthData.Minute<0)
        {
            NorthSouthData.Degree--;
            NorthSouthData.Minute+=60;
        }



        if(EastWestData.Degree >= 180)
        {
            EastWestData = SwitchDirection(EastWestData);
        }


        

        //kuzey-güney olarak 90 dan fazla gitmeyi düşünmediğimiz için
        //ayarlamıyoruz etmiyoruz.

        latitude.Value = NorthSouthData; 
        longitude.Value = EastWestData; 
        
        //Debug.Log("Yeni pozisyon" + NorthSouthData  + " " +EastWestData);
        
    }

    public float CalculateDistanceBetweenTwoPoints(Coordinate latitude1,Coordinate longitude1 ,Coordinate latitude2,Coordinate longitude2)
    {
        // kaynak : https://stackoverflow.com/questions/27928/calculate-distance-between-two-latitude-longitude-points-haversine-formula
        float lat1 = latitude1.Degree + latitude1.Minute / 60 + latitude1.Second / 3600;
        float lat2 = latitude2.Degree + latitude2.Minute / 60 + latitude2.Second / 3600;
        float lon1 = longitude1.Degree + longitude1.Minute / 60 + longitude1.Second / 3600;
        float lon2 = longitude2.Degree + longitude2.Minute / 60 + longitude2.Second / 3600;

        if(latitude1.Direction == GlobalDirections.South)
            lat1*=-1;
        if(latitude2.Direction == GlobalDirections.South)
            lat2*=-1;
        if(longitude1.Direction == GlobalDirections.West)
            lon1*=-1;
        if(longitude2.Direction == GlobalDirections.West)
            lon2*=-1;
        

        var dLat = Mathf.Deg2Rad *(lat2-lat1);  // deg2rad below
        var dLon = Mathf.Deg2Rad *(lon2-lon1); 
        var a = 
            Mathf.Sin(dLat/2) * Mathf.Sin(dLat/2) +
            Mathf.Cos(Mathf.Deg2Rad * (lat1)) * Mathf.Cos(Mathf.Deg2Rad * (lat2)) * 
            Mathf.Sin(dLon/2) * Mathf.Sin(dLon/2)
            ; 
        var c = 2 * Mathf.Atan2(Mathf.Sqrt(a), Mathf.Sqrt(1-a)); 
        var d = EarthRadius * c; // Distance in km
        return d;
        
    } 

    public Vector3 CalculateDirectionBetweenTwoPoints(Coordinate latitude1,Coordinate longitude1 ,Coordinate latitude2,Coordinate longitude2)
    {
        float lat1 = latitude1.Degree + latitude1.Minute / 60 + latitude1.Second / 3600;
        float lat2 = latitude2.Degree + latitude2.Minute / 60 + latitude2.Second / 3600;
        float lon1 = longitude1.Degree + longitude1.Minute / 60 + longitude1.Second / 3600;
        float lon2 = longitude2.Degree + longitude2.Minute / 60 + longitude2.Second / 3600;
        
        if(latitude1.Direction == GlobalDirections.South)
            lat1*=-1;
        if(latitude2.Direction == GlobalDirections.South)
            lat2*=-1;
        if(longitude1.Direction == GlobalDirections.West)
            lon1*=-1;
        if(longitude2.Direction == GlobalDirections.West)
            lon2*=-1;
        
        float latRad = ((lat1 + lat2) * 0.5f) * Mathf.Deg2Rad;

        float x = (lon2 - lon1) * Mathf.Cos(latRad);
        
        float y = lat2 - lat1;
        
        return new Vector3(x,0 ,y).normalized;
        
    } 

}