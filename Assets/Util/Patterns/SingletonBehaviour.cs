using UnityEngine;

public class SingletonBehaviour<T> : MonoBehaviour where T : SingletonBehaviour<T>
{
    public static T Instance { get; private set; }
    protected virtual void OnEnable()
    {
        Instance = (T)this;
    }

    protected virtual void OnDisable()
    {
        Instance = Instance == this ? null : Instance;        
    }
}