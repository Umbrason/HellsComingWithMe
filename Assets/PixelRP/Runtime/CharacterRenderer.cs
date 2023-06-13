
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer)), ExecuteAlways]
public class CharacterRenderer : MonoBehaviour
{
    void OnEnable()
    {
        GetComponent<SpriteRenderer>().renderingLayerMask = 1 << 1;
    }    
}
