public interface IHandInput : IInteractable
{
    abstract public void OnHandInput(float xValue, float yValue);
    abstract public void OnRightHandInput(float xValue, float yValue);
    abstract public void OnButtonInput();


}
