using System.Collections.Generic;
using DogWater;
using Unity.Netcode;
using UnityEngine;

public interface IHoldableItem : IInteractable
{
    public void PickUp(Transform holder);
    public void Drop();

}