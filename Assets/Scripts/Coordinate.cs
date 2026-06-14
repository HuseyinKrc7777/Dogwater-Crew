using System;
using System.Runtime.ExceptionServices;
using Unity.Netcode;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

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

