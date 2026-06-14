using Unity.Netcode;
using UnityEngine;

public class GlobalCoordinate : NetworkBehaviour
{
    //const int EarthRadius = 6367449;
    //test amaçlı dünya büyüklüğü küçüktür.
    const int EarthRadius = 63;
    public NetworkVariable<Coordinate> latitude = new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<Coordinate> longtitude = new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
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
    public GlobalCoordinate(Coordinate latitude , Coordinate longtitude)
    {
        this.latitude.Value = latitude;
        this.longtitude.Value = longtitude;
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
        longtitude.Value = lo;
    }

    public float LongtitudeSecondLength(Coordinate northSouth)
    {
        float degree = northSouth.Degree + northSouth.Minute / 60f + northSouth.Second / 3600f;
        degree *= Mathf.Deg2Rad ;
        return (float)(Mathf.PI / 180 * EarthRadius * Mathf.Cos(degree) / 3600f);
    }
    public float LatitudeSecondLength(Coordinate northSouth)
    {
        float degree = northSouth.Degree + northSouth.Minute / 60f + northSouth.Second / 3600f;
        float phi = degree * Mathf.Deg2Rad;
        float baseRadius = 6367449; // reference Earth radius
        float scale = EarthRadius / baseRadius;

        return (111132.95255f
            - 559.84957f * Mathf.Cos(2f * phi)
            + 1.17514f * Mathf.Cos(4f * phi)
            - 0.00230f * Mathf.Cos(6f * phi) ) / 3600f * scale;
    }
    public void ChangePosition(Coordinate newLatitude , Coordinate newLongtitude)
    {
        
        Coordinate NorthSouthData = newLatitude;
        Coordinate EastWestData = newLongtitude;

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
        longtitude.Value = EastWestData; 
        
        //Debug.Log("Yeni pozisyon" + NorthSouthData  + " " +EastWestData);
        
    }


}