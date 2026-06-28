using System;
using System.Runtime.ExceptionServices;
using Unity.Netcode;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
[Serializable]
public struct Coordinate : INetworkSerializable
{
    public GlobalDirections Direction;
    public int Degree;
    public int Minute;
    public int Second;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref Direction);
        serializer.SerializeValue(ref Degree);
        serializer.SerializeValue(ref Minute);
        serializer.SerializeValue(ref Second);
    }
    public override string ToString()
    {
        return $"{Direction} {Degree}° {Minute}' {Second}\"";
    }
}
public enum GlobalDirections
{
    North,
    South,
    East,
    West,
}
//TODO kordinat şeysi alttaki sisteme değiştirilecek
[Serializable]
public struct LatitudeCoordinate : INetworkSerializable
{
    public int Degree;
    public int Minute;
    public int Second;

    public readonly float GetDegree()
    {
        return Degree + Minute / 60.0f + Second / 3600.0f;
    }
    public readonly float GetMinute()
    {
        return Degree * 60.0f + Minute + Second / 60.0f;
    }
    public readonly float GetSecond()
    {
        return Degree * 3600.0f + Minute *60.0f + Second;
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
        return $" {Degree}° {Minute}' {Second}\"";
    }
}

[Serializable]
public struct LongitudeCoordinate : INetworkSerializable
{
    public int Degree;
    public int Minute;
    public int Second;

     public readonly float GetDegree()
    {
        return Degree + Minute / 60.0f + Second / 3600.0f;
    }
    public readonly float GetMinute()
    {
        return Degree * 60.0f + Minute + Second / 60.0f;
    }
    public readonly float GetSecond()
    {
        return Degree * 3600.0f + Minute *60.0f + Second;
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
        return $" {Degree}° {Minute}' {Second}\"";
    }
}
