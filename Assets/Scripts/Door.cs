using UnityEngine;

public class Door : MonoBehaviour
{
    public enum DoorState { Open, Closed }
    public DoorState doorState = DoorState.Closed;

    public void Open()
    {
        doorState = DoorState.Open;
    }
    public void Close()
    {
        doorState = DoorState.Closed;
    }
}