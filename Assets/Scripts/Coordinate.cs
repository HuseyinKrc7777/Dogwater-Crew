using System;
using System.Runtime.ExceptionServices;
using JetBrains.Annotations;
using Unity.Netcode;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

public interface ICoordinate : INetworkSerializable
{
    abstract public float GetDegree();
    abstract public float GetMinute();
    abstract public float GetSecond();
    abstract public void AddSecond(int second);
    abstract public void AddMinute(int second);
    abstract public void AddDegree(int second);

    abstract public string ToString();

}
[Serializable]
public struct LatitudeCoordinate : ICoordinate
{
    public int Degree;
    public int Minute;
    public int Second;
    public float GetDegree()
    {
        return Degree + Minute / 60.0f + Second / 3600.0f;
    }
    public float GetMinute()
    {
        return Degree * 60.0f + Minute + Second / 60.0f;
    }
    public float GetSecond()
    {
        return Degree * 3600.0f + Minute * 60.0f + Second;
    }

    public void AddSecond(int second)
    {
        Second += second;
        int multiplier = second > 0 ? 1 : -1;
        while (Second > 60 || Second < -60)
        {
            Minute += multiplier;
            Second -= 60 * multiplier;
        }
        while (Minute > 60 || Minute < -60)
        {
            Degree += multiplier;
            Minute -= 60 * multiplier;
        }
    }
    public void AddMinute(int minute)
    {
        Minute += minute;
        int multiplier = minute > 0 ? 1 : -1;
        while (Minute > 60 || Minute < -60)
        {
            Degree += multiplier;
            Minute -= 60 * multiplier;
        }
    }
    public void AddDegree(int degree)
    {
        Degree += degree;
    }
    public void NetworkSerialize<T>(BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref Degree);
        serializer.SerializeValue(ref Minute);
        serializer.SerializeValue(ref Second);
    }
    public override string ToString()
    {
        return $" Latittude : {Degree}° {Minute}' {Second}\" ";
    }
}

[Serializable]
public struct LongitudeCoordinate : ICoordinate
{
    public int Degree;
    public int Minute;
    public int Second;
    public float GetDegree()
    {
        return Degree + Minute / 60.0f + Second / 3600.0f;
    }
    public float GetMinute()
    {
        return Degree * 60.0f + Minute + Second / 60.0f;
    }
    public float GetSecond()
    {
        return Degree * 3600.0f + Minute * 60.0f + Second;
    }
    public void AddSecond(int second)
    {
        Second += second;
        int multiplier = second > 0 ? 1 : -1;
        while (Second > 60 || Second < -60)
        {
            Minute += multiplier;
            Second -= 60 * multiplier;
        }
        while (Minute > 60 || Minute < -60)
        {

            Degree += multiplier;
            Minute -= 60 * multiplier;
        }
        if (Degree > 180 || Degree < -180)
            switchDirection();
    }
    public void AddMinute(int minute)
    {
        Minute += minute;
        int multiplier = minute > 0 ? 1 : -1;

        while (Minute > 60 || Minute < -60)
        {
            Degree += multiplier;
            Minute -= 60 * multiplier;
        }
        if (Degree > 180 || Degree < -180)
            switchDirection();
    }
    public void AddDegree(int degree)
    {
        Degree += degree;
        if (Degree > 180 || Degree < -180)
            switchDirection();
    }



    public void NetworkSerialize<T>(BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref Degree);
        serializer.SerializeValue(ref Minute);
        serializer.SerializeValue(ref Second);
    }
    public override string ToString()
    {
        return $"Longtitude : {Degree}° {Minute}' {Second}\" ";
    }

    private void switchDirection()
    {
        int InnerMultiplier = Degree > 0 ? 1 : -1;
        int extraDegree = Degree - 180 * InnerMultiplier;
        int extraMinute = Minute;
        int extraSecond = Degree;
        Degree = -180 * InnerMultiplier;
        AddSecond(extraDegree * 3600 + extraMinute * 60 + extraSecond);

    }
}
