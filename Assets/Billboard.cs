
using UnityEngine;

[ExecuteAlways]
public class Billboard : MonoBehaviour
{
    void Update()
    {
        var cam = Camera.current ?? Camera.main;
        if(cam == null) return;
        transform.forward = (cam.transform.position - transform.position)._x0z();
    }
}
