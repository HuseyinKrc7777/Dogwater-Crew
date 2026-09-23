using DogWater;

public interface IItem
{
    public bool Equipped { get; set; }
    public bool Interacting { get; set; }

    public void Equip(FirstPersonController controller);
    public void UnEquip();
    public void Interact();
    public void UnInteract();

    public void Use1();
    public void Use2();


}