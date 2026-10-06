using UnityEngine;
using System;
using DogWater;

public class FishingRod : IItem
{
    private bool _equipped;
    private bool _interacting;
    private Player player;

    public bool Equipped { get => _equipped; set => _equipped = value; }
    public bool Interacting { get => _interacting; set => _interacting = value;}
    public void Equip(FirstPersonController controller)
    {
        
    }

    public void Interact()
    {
        
    }

    public void UnEquip()
    {
        
    }

    public void UnInteract()
    {
        
    }

    public void Use1()
    {
        
    }

    public void Use2()
    {
        
    }
}

