using System;
using DogWater;
using UnityEngine;
public class Lantern : IItem
{
    private bool _equipped;
    private bool _interacting;
    private Player player;

    public bool Equipped { get => _equipped; set => _equipped = value; }
    public bool Interacting { get => _interacting; set => _interacting = value; }
    public void Equip(FirstPersonController controller)
    {
        //burada 
        player = controller.GetComponent<Player>();
        player.LanternRpc(true);
    }

    public void Interact()
    {
        //throw new NotImplementedException();
    }

    public void UnEquip()
    {
        player.LanternRpc(false);
        //throw new NotImplementedException();
    }

    public void UnInteract()
    {
        //throw new NotImplementedException();
    }

    public void Use1()
    {
        //throw new NotImplementedException();
    }

    public void Use2()
    {
        //throw new NotImplementedException();
    }
}