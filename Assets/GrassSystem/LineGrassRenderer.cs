using System;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class LineGrassRenderer : MonoBehaviour
{
    const float OcttreeSize = 40f;

    private class GrassData
    {
        public Matrix4x4 objectToWorld;
        public float burnTime;
        public GrassData(Matrix4x4 TRS)
        {
            this.objectToWorld = TRS;
            burnTime = -1f;
        }
    }
    private static readonly Dictionary<Vector3Int, SpatialOcttree<GrassData>> grassChunks = new();

    public static void AddGrass(Vector3 position, Vector3 normal)
    {
        var TRS = Matrix4x4.TRS(position, Quaternion.FromToRotation(Vector3.up, normal), Vector3.one);
        var grassData = new GrassData(TRS);
        var chunkKey = GrassChunkKey(position);
        var chunk = grassChunks.ContainsKey(chunkKey) ? grassChunks[chunkKey] : (grassChunks[chunkKey] = new(KeyToChunkCenter(chunkKey), OcttreeSize));
        chunk.Add(position, new(TRS));
    }

    public static void RemoveGrass(Vector3 position)
    {
        var key = GrassChunkKey(position);
        if (!grassChunks.ContainsKey(key)) return;
        var chunk = grassChunks[key];
        chunk.Remove(position);
        if (chunk.Count == 0) grassChunks.Remove(key);
    }

    private static Vector3Int GrassChunkKey(Vector3 key)
    {
        key /= OcttreeSize;
        return Vector3Int.RoundToInt(key);
    }

    public static void BurnGrassLine(Ray ray, float distance, float radius)
    {
        var items = new List<GrassData>();
        foreach (var chunk in grassChunks)
            chunk.Value.GetItemsInThickRay(ray, distance, radius, items);
        for (int i = 0; i < items.Count; i++)
            items[i].burnTime = items[i].burnTime > 0 ? items[i].burnTime : Time.time;
    }

    public static void BurnGrassSphere(Vector3 center, float radius)
    {
        var items = new List<GrassData>();
        foreach (var chunk in grassChunks)
            chunk.Value.GetItemsInRadius(center, radius, items);
        for (int i = 0; i < items.Count; i++)
            items[i].burnTime = items[i].burnTime > 0 ? items[i].burnTime : Time.time;
    }

    private static Vector3 KeyToChunkCenter(Vector3Int key)
    {
        return (Vector3)key * OcttreeSize;
    }

    [field: SerializeField] public Material Material { get; set; }
    [field: SerializeField] public int BladeCount { get; set; } = 100;
    [field: SerializeField] public float GroupRadius { get; set; } = .25f;
    public AnimationCurve BladeHeightCurve;

    private Mesh cached_grassGroupMesh;
    private Mesh GrassGroupMesh
    {
        get
        {
            if (cached_grassGroupMesh) return cached_grassGroupMesh;
            cached_grassGroupMesh = new Mesh();
            var verts = new Vector3[4 * BladeCount];
            var vertexColors = new Color32[4 * BladeCount];
            var lines = new int[6 * BladeCount];
            UnityEngine.Random.InitState(0);
            for (int i = 0; i < BladeCount; i++)
            {
                var offset = UnityEngine.Random.insideUnitCircle._x0y() * GroupRadius;
                var height = BladeHeightCurve.Evaluate(offset.magnitude / GroupRadius);
                verts[i * 4] = offset;
                verts[i * 4 + 1] = Vector3.up * .33f * height + offset;
                verts[i * 4 + 2] = Vector3.up * .66f * height + offset;
                verts[i * 4 + 3] = Vector3.up * height + offset;
                var color = UnityEngine.Random.ColorHSV(0, 1, 1, 1);
                vertexColors[i * 4] = color;
                vertexColors[i * 4 + 1] = color;
                vertexColors[i * 4 + 2] = color;
                vertexColors[i * 4 + 3] = color;
                lines[i * 6] = 0 + i * 4;
                lines[i * 6 + 1] = 1 + i * 4;
                lines[i * 6 + 2] = 1 + i * 4;
                lines[i * 6 + 3] = 2 + i * 4;
                lines[i * 6 + 4] = 2 + i * 4;
                lines[i * 6 + 5] = 3 + i * 4;
            }

            cached_grassGroupMesh.vertices = verts;
            cached_grassGroupMesh.colors32 = vertexColors;
            cached_grassGroupMesh.SetIndices(lines, MeshTopology.Lines, 0);
            return cached_grassGroupMesh;
        }
    }

    void OnValidate()
    {
        cached_grassGroupMesh = null;
    }

    private List<GrassData> grassData = new();


    private const int BATCH_SIZE = 500;


    void Update()
    {
        foreach (var chunk in grassChunks.Values)
        {
            if (chunk.Count == 0) continue;
            grassData.Clear();
            chunk.GetAll(grassData);
            var offset = 0;
            while (offset < grassData.Count)
            {
                var count = Mathf.Min(grassData.Count - offset, BATCH_SIZE);
                var resources = (availableGrassBatchResources.Count > 0) ? availableGrassBatchResources.Dequeue() : new();
                usedGrassBatchResources.Enqueue(resources);
                for (int i = 0; i < count; i++)
                {
                    resources.matrices[i] = grassData[i + offset].objectToWorld;
                    resources.burnTimes[i] = grassData[i + offset].burnTime;
                }
                offset += BATCH_SIZE;
                RenderGrassBatch(resources, count);
            }
        }
        while (usedGrassBatchResources.Count > 0)
            availableGrassBatchResources.Enqueue(usedGrassBatchResources.Dequeue());
    }

    void OnDisable()
    {
        grassChunks.Clear();
        while (availableGrassBatchResources.Count > 0)
            availableGrassBatchResources.Dequeue().Release();
        while (usedGrassBatchResources.Count > 0)
            usedGrassBatchResources.Dequeue().Release();
    }

    private class GrassDrawingResources
    {
        public readonly float[] burnTimes = new float[BATCH_SIZE];
        public readonly Matrix4x4[] matrices = new Matrix4x4[BATCH_SIZE];
        public readonly ComputeBuffer buffer = new ComputeBuffer(BATCH_SIZE, sizeof(float));
        public readonly MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
        public void Release() => buffer.Release();
    }

    private readonly Queue<GrassDrawingResources> availableGrassBatchResources = new();
    private readonly Queue<GrassDrawingResources> usedGrassBatchResources = new();

    void RenderGrassBatch(GrassDrawingResources resources, int count)
    {
        var propertyBlock = new MaterialPropertyBlock();
        resources.propertyBlock.SetFloatArray("_BurnTime", resources.burnTimes);
        Graphics.DrawMeshInstanced(GrassGroupMesh, 0, Material, resources.matrices, count, resources.propertyBlock);
    }
}
