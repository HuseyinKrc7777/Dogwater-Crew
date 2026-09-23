using System;
using DogWater;
using UnityEngine;
[ExecuteAlways]
public class Compass : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public Transform stick;
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    { 
        Vector3 forw = Vector3.forward;
        if(transform.parent!=null)
        {
            forw = transform.parent.InverseTransformDirection(forw);
        }
        stick.localRotation = Quaternion.LookRotation(forw);
    }

}


public class EquipableCompass : IItem
{
    private bool _equipped;
    private bool _interacting;
    private Player player;

    [SerializeField] Transform CompassObject;

    public bool Equipped { get => _equipped; set => _equipped = value; }
    public bool Interacting { get => _interacting; set => _interacting = value; }
    public void Equip(FirstPersonController controller)
    {
        //burada 
        player = controller.GetComponent<Player>();
        player.CompassRpc(true);
        //throw new NotImplementedException();
    }

    public void Interact()
    {
        //TODO pusulayı ekrana yaklaştırıp açıları daha okunablir yapılacak
        //throw new NotImplementedException();
    }

    public void UnEquip()
    {
        player.CompassRpc(false);

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

