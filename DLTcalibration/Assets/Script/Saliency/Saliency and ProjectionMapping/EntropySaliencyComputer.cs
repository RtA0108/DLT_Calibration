using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class EntropySaliencyComputer
{
    public static Dictionary<Vector3, float> Compute(MeshFilter meshFilter, List<Vector3> visibleVertices, float l)
    {
        Dictionary<Vector3, float> entropyMap = new Dictionary<Vector3, float>();
        float[] entropyValues = ComputeVertexEntropy(visibleVertices, l, meshFilter);

        for (int i = 0; i < visibleVertices.Count; i++)
            entropyMap[visibleVertices[i]] = entropyValues[i];

        return entropyMap;
    }

    private static float[] ComputeVertexEntropy(List<Vector3> visibleVertices, float l, MeshFilter meshFilter)
    {
        float[] entropyValues = new float[visibleVertices.Count];
        Vector3[] vertexArray = visibleVertices.ToArray();
        
        float[] sigmaScales = new float[] { 0.05f * l, 0.1f * l, 0.2f * l };
        float sigma_max = sigmaScales.Max();
        
        for (int i = 0; i < visibleVertices.Count; i++)
        {
            List<Vector3> allNeighbors = GetNeighborsByEuclideanDistance(visibleVertices[i], sigma_max, vertexArray);

            float aggregatedEntropy = 0f;
            float totalWeight = 0f;

            foreach (float sigma in sigmaScales)
            {
                var neighbors = allNeighbors.Where(v => Vector3.Distance(v, visibleVertices[i]) <= sigma).ToList();
                if (neighbors.Count == 0) continue;

                Dictionary<Vector3Int, int> normalHistogram = new Dictionary<Vector3Int, int>();

                foreach (var neighbor in neighbors)
                {
                    Vector3 normal = GetVertexNormal(meshFilter, neighbor);
                    Vector3Int quantizedNormal = QuantizeNormal(normal, 1000);

                    if (!normalHistogram.ContainsKey(quantizedNormal))
                        normalHistogram[quantizedNormal] = 0;

                    normalHistogram[quantizedNormal]++;
                }

                float entropy = 0f;
                int totalNormals = neighbors.Count;

                foreach (var count in normalHistogram.Values)
                {
                    float probability = (float)count / (totalNormals + 1e-6f);
                    if (probability > 0)
                        entropy -= probability * Mathf.Log(probability + 1e-6f);
                }

                float maxEntropy = Mathf.Log(neighbors.Count + 1e-6f);
                float normalizedEntropy = entropy / maxEntropy;

                float weight = 1.0f / sigma;
                aggregatedEntropy += normalizedEntropy * weight;
                totalWeight += weight;
            }

            entropyValues[i] = aggregatedEntropy / totalWeight;
        }

        return entropyValues;
    }

    private static List<Vector3> GetNeighborsByEuclideanDistance(Vector3 position, float sigma, Vector3[] vertices)
    {
        List<Vector3> neighbors = new List<Vector3>();
        float sigmaSquared = sigma * sigma;

        foreach (Vector3 vertex in vertices)
        {
            float distSqr = (position - vertex).sqrMagnitude;
            if (distSqr > 0f && distSqr <= sigmaSquared)
                neighbors.Add(vertex);
        }

        return neighbors;
    }

    private static Vector3 GetVertexNormal(MeshFilter meshFilter, Vector3 vertexPosition)
    {
        Vector3[] positions = meshFilter.mesh.vertices;
        Vector3[] normals = meshFilter.mesh.normals;

        for (int i = 0; i < positions.Length; i++)
        {
            if (Vector3.Distance(meshFilter.transform.TransformPoint(positions[i]), vertexPosition) < 0.001f)
                return normals[i];
        }

        return Vector3.zero;
    }

    private static Vector3Int QuantizeNormal(Vector3 normal, int scale)
    {
        return new Vector3Int(
            Mathf.RoundToInt(normal.x * scale),
            Mathf.RoundToInt(normal.y * scale),
            Mathf.RoundToInt(normal.z * scale)
        );
    }
}