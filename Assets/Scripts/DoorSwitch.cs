using UnityEngine;

public class DoorSwitch : MonoBehaviour
{
    [Tooltip("The Door component controlled by this switch.")]
    public Door targetDoor;

    private void Start()
    {
        AutoFindTargetDoor();
    }

    public void AutoFindTargetDoor()
    {
        if (targetDoor != null) return;

        Door[] allDoors = FindObjectsByType<Door>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var d in allDoors)
        {
            if (d != null && d.toggleSwitch == this.gameObject)
            {
                targetDoor = d;
                break;
            }
        }
    }

    // Click dispatched by AirflowPlacementManager — single source of truth
    public void OnClick()
    {
        if (targetDoor == null) AutoFindTargetDoor();

        if (targetDoor != null)
            targetDoor.Toggle();
        else
            Debug.LogWarning($"[DoorSwitch] Target door not assigned on {gameObject.name}!");
    }
}
