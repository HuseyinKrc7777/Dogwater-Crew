using System.Collections.Generic;
using DogWater;
using Unity.Netcode;
using UnityEngine;

public interface IHoldableItem : IInteractable
{
    public IHoldableItem originalItem{get;set;}
    public void PickUp(Transform holder);
    public void Drop();

}