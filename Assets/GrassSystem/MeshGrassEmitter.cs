using UnityEngine;

[ExecuteAlways]
public class MeshGrassEmitter : MonoBehaviour
{
    [field: SerializeField] public Mesh EmissionShape { get; set; }
    [field: SerializeField] public int GrassCount { get; set; }

    void Start()
    {
        OnValidate();
    }

    private Grass[] GrassObjects;

    private struct Grass
    {
        public Vector3 position;
        public Vector3 normal;
        public Grass(Vector3 position, Vector3 normal)
        {
            this.position = position;
            this.normal = normal;
        }
    }

    void OnValidate()
    {
        if (GrassObjects != null)
            for (int i = 0; i < GrassObjects.Length; i++)
                LineGrassRenderer.RemoveGrass(GrassObjects[i].position);
        GrassObjects = GenerateGrass();
        for (int i = 0; i < GrassObjects.Length; i++)
            LineGrassRenderer.AddGrass(GrassObjects[i].position, GrassObjects[i].normal);
    }

    void OnDisable()
    {
        for (int i = 0; i < GrassObjects.Length; i++)
            LineGrassRenderer.RemoveGrass(GrassObjects[i].position);
    }

    private Grass[] GenerateGrass()
    {
        if (GrassCount < 0) return new Grass[0];
        Random.InitState(0);
        var rays = GeometryMath.GetRandomRaysOnMesh(EmissionShape, GrassCount);
        var grassObjects = new Grass[GrassCount];
        for (int i = 0; i < GrassCount; i++)
        {
            var position = transform.localToWorldMatrix.MultiplyPoint(rays[i].origin);
            var normal = transform.localToWorldMatrix.MultiplyVector(rays[i].direction);
            grassObjects[i] = new(position, normal);
        }
        return grassObjects;
    }
}
