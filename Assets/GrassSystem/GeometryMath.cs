using UnityEngine;

public static class GeometryMath
{
    public static Ray[] GetRandomRaysOnMesh(Mesh mesh, int count)
    {
        if (count <= 0 || mesh == null) return new Ray[0];
        var triangleAreas = new float[mesh.triangles.Length / 3];
        var totalArea = 0f;
        for (int i = 0; i < triangleAreas.Length; i++)
        {
            Vector3 v1 = mesh.vertices[mesh.triangles[i * 3]];
            Vector3 v2 = mesh.vertices[mesh.triangles[i * 3 + 1]];
            Vector3 v3 = mesh.vertices[mesh.triangles[i * 3 + 2]];
            triangleAreas[i] = Vector3.Cross(v2 - v1, v3 - v1).magnitude / 2;
            totalArea += triangleAreas[i];
        }
        var rays = new Ray[count];
        for (int i = 0; i < count; i++)
            rays[i] = GetRandomRayOnMesh(mesh, triangleAreas, totalArea);
        return rays;
    }

    public static Ray GetRandomRayOnMesh(Mesh mesh, float[] triangleAreas, float totalArea)
    {
        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;

        float randomValue = Random.Range(0, totalArea);
        int triangleIndex = 0;
        float accumulatedArea = 0;
        while (triangleIndex < triangleAreas.Length - 1 && randomValue > accumulatedArea + triangleAreas[triangleIndex])
        {
            accumulatedArea += triangleAreas[triangleIndex];
            triangleIndex++;
        }

        // Choose a random point within the triangle
        Vector3 v1 = vertices[triangles[triangleIndex * 3]];
        Vector3 v2 = vertices[triangles[triangleIndex * 3 + 1]];
        Vector3 v3 = vertices[triangles[triangleIndex * 3 + 2]];
        Vector2 barycentricCoords = GetRandomBarycentricCoords();
        Vector3 randomPointInTriangle = barycentricCoords.x * v1 + barycentricCoords.y * v2 + (1 - barycentricCoords.x - barycentricCoords.y) * v3;
        Vector3 normal = Vector3.Cross(v2 - v1, v3 - v1).normalized;

        return new Ray(randomPointInTriangle, normal);
    }

    public static Vector2 GetRandomBarycentricCoords()
    {
        var x = Random.Range(0f, 1f);
        var y = Random.Range(0f, 1f);
        if (x + y > 1)
        {
            y = 1 - y;
            x = 1 - x;
        }
        return new Vector2(x, y);
    }
}
