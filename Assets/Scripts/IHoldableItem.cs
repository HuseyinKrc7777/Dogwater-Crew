using System.Collections.Generic;
using DogWater;
using Unity.Netcode;
using UnityEngine;

public interface IHoldableItem : IInteractable
{
    public ulong NetworkObjectId{get;set;}
    public IHoldableItem originalItem{get;set;}
    public GameObject spawnedMesh{get;set;}

    public void PickUp(Transform holder);
    public void Drop(Vector3 Position);

}