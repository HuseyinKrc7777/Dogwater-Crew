using System;
using System.Runtime.ExceptionServices;
using JetBrains.Annotations;
using Unity.Netcode;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
[Serializable]
public struct CoordinateData
{
    //İnsan okunabilirliği için 3 değer tutuluyor , eğer performans sorun çıkarırsa sadece saniye tutmaya geçilebilir 
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

    public int GetSecond()
    {
        return Degree * 3600 + Minute * 60 + Second;
    }

    public void SetFromTotalSeconds(int totalSeconds)
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
    public override string ToString()
    {
        return $"{Degree}° {Mathf.Abs(Minute):00}' {Mathf.Abs(Second):00}\"";
    }
}
[Serializable]
public struct LatitudeCoordinate : INetworkSerializable
{
    public CoordinateData data;

    public float GetDegree() => data.GetDegree();

    public void AddSecond(int value)
    {
        data.AddSecond(value);

        int seconds = data.GetSecond();
        seconds = Mathf.Clamp(seconds, -90 * 60 * 60, 90 * 60 * 60);

        data.SetFromTotalSeconds(seconds);
    }


    public void NetworkSerialize<T>(BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref data.Degree);
        serializer.SerializeValue(ref data.Minute);
        serializer.SerializeValue(ref data.Second);
    }
    
    public override string ToString()
    {
        return data.ToString();
    }
}
[Serializable]
public struct LongitudeCoordinate : INetworkSerializable
{
    public CoordinateData data;

    public float GetDegree() => data.GetDegree();

    public void AddSecond(int value)
    {
        data.AddSecond(value);

        Normalize();
    }


    private void Normalize()
    {
        int seconds = data.GetSecond();

        while (seconds >= 180 * 3600)
            seconds -= 360 * 3600;

        while (seconds < -180 * 3600)
            seconds += 360 * 3600;

        data.SetFromTotalSeconds(seconds);
    }


    public void NetworkSerialize<T>(BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref data.Degree);
        serializer.SerializeValue(ref data.Minute);
        serializer.SerializeValue(ref data.Second);
    }

    public override string ToString()
    {
        return data.ToString();
    }
    
}


