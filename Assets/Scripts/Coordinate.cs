using System;
using System.Runtime.ExceptionServices;
using JetBrains.Annotations;
using Unity.Netcode;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
[Serializable]
public abstract class Coordinate 
{
    public int Degree;
    public int Minute;
    public int Second;
    public float GetDegree()
    {
        int sign = Degree < 0 || Minute < 0 || Second < 0 ? -1 : 1;

        return sign * (
            Mathf.Abs(Degree) +
            Mathf.Abs(Minute) / 60f +
            Mathf.Abs(Second) / 3600f
        );
    }
    public float GetMinute()
    {
        return Degree * 60 + Minute + Second / 60.0f;
    }
    public int GetSecond()
    {
        return Degree * 3600 + Minute * 60 + Second;
    }
    public void AddSecond(int second)
    {
        SetFromTotalSeconds(GetSecond() + second);
    }

    public void AddMinute(int minute)
    {
        SetFromTotalSeconds(GetSecond() + minute * 60);
    }

    public void AddDegree(int degree)
    {
        SetFromTotalSeconds(GetSecond() + degree * 3600);
    }
    protected virtual void SetFromTotalSeconds(int totalSeconds)
    {
        int sign = totalSeconds < 0 ? -1 : 1;

        int abs = Mathf.Abs(totalSeconds);

        Degree = abs / 3600;
        Minute = abs % 3600 / 60;
        Second = abs % 60;

        Degree *= sign;
        Minute *= sign;
        Second *= sign;
    }

    
    public override string ToString()
    {
        return $": {Degree}° {Minute}' {Second}\" ";
    }

}
[Serializable]
public class LatitudeCoordinate : Coordinate ,INetworkSerializable
{
   
    protected override void SetFromTotalSeconds(int totalSeconds)
    {
        totalSeconds = Mathf.Clamp(totalSeconds,-90*60*60,90*60*60);
        base.SetFromTotalSeconds(totalSeconds);
    }
    public void NetworkSerialize<T>(BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref Degree);
        serializer.SerializeValue(ref Minute);
        serializer.SerializeValue(ref Second);
    }
    
}

[Serializable]
public class LongitudeCoordinate : Coordinate , INetworkSerializable
{

    protected override void SetFromTotalSeconds(int totalSeconds)
    {
        base.SetFromTotalSeconds(totalSeconds);
        NormalizeDegree();
    }

    private void NormalizeDegree()
    {
        while (Degree >= 180)
            Degree -= 360;

        while (Degree < -180)
            Degree += 360;
    }
    public void NetworkSerialize<T>(BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref Degree);
        serializer.SerializeValue(ref Minute);
        serializer.SerializeValue(ref Second);
    }

}
