using UnityEngine;
using Unity.Scripting.LifecycleManagement;

public class MainGameObjectCamera : MonoBehaviour
{
    public static Camera Instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
    static void ResetStatics()
    {
        Instance = null;
    }
    
    void Awake()
    {
        Instance = GetComponent<Camera>();
    }
}
