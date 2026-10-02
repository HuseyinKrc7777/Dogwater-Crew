using UnityEngine;

public class holdabkeobjectmeshextension : MonoBehaviour , IHoldableItem
{
    protected IHoldableItem _originalItem;

    IHoldableItem IHoldableItem.originalItem { get => _originalItem; set => _originalItem = value; }

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

}
