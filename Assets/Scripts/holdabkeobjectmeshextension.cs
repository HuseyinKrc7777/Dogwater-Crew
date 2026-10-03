using Unity.VisualScripting;
using UnityEngine;

public class holdabkeobjectmeshextension : MonoBehaviour , IHoldableItem
{
    protected IHoldableItem _originalItem;

    IHoldableItem IHoldableItem.originalItem { get => _originalItem; set => _originalItem = value; }
    public bool stuckToFloor = true;
    public void Drop()
    {
        _originalItem.Drop();
    }

    public void OnInteract(Player player)
    {
        _originalItem.OnInteract(player);
    }

    public void OnUnInteract(Player player)
    {
        _originalItem.OnUnInteract(player);

    }

    public void PickUp(Transform holder)
    {
        _originalItem.PickUp(holder);
        Destroy(this);

    }
    public void floatObject()
    {
        if (WaterController.Instance == null)
            return;

        Vector3? temp = WaterController.Instance.GetWave(transform.position);
        Vector3 wave = new();
        if(temp!=null)
            wave = (Vector3)temp;
        else
            wave = transform.position;
        Vector3 pos = new Vector3(transform.position.x,wave.y,transform.position.z);
        Vector3 newPos = Vector3.Slerp(transform.position,pos,Time.deltaTime*5);
        transform.position = newPos;
    }
    

    public void FixedUpdate()
    {
        if(!stuckToFloor)
        {
            floatObject();
        }
    }

}
