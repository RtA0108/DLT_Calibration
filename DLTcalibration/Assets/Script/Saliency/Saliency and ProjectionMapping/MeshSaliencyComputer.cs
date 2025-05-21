using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class MeshSaliencyComputer
{
    public static Dictionary<Vector3, float> Compute(MeshFilter meshFilter, List<Vector3> vertices, float l)
    {
        Vector3[] meshVertices = meshFilter.mesh.vertices;
        Vector3[] worldPositions = meshVertices.Select(v => meshFilter.transform.TransformPoint(v)).ToArray();
        Dictionary<int, float> curvatureMap = ComputeMeanCurvatures(meshFilter);
        //Dictionary<int, float> curvatureMap = ComputeMeanCurvatures_Taubin(meshFilter);
        float epsilon = 0.01f * l; // 또는 0.02f 실험
        float[] sigmaScales = new float[] { 2f, 3f, 4f, 5f, 6f }.Select(s => s * epsilon).ToArray();

        Dictionary<Vector3, float[]> multiScaleSaliency = new();
        Dictionary<Vector3, float> finalSaliency = new();

        foreach (float sigma in sigmaScales)
        {
            foreach (Vector3 v in vertices)
            {
                int index = SaliencyUtils.FindNearestVertexIndex(v, worldPositions);
                float g1 = ComputeGaussianWeightedAverage(index, worldPositions, curvatureMap, sigma);
                float g2 = ComputeGaussianWeightedAverage(index, worldPositions, curvatureMap, 2f * sigma);

                float saliency = Mathf.Abs(g1 - g2);
                if (!multiScaleSaliency.ContainsKey(v))
                    multiScaleSaliency[v] = new float[sigmaScales.Length];
                multiScaleSaliency[v][System.Array.IndexOf(sigmaScales, sigma)] = saliency;
            }
        }

        ApplyNonlinearSuppression(multiScaleSaliency, out finalSaliency);
        return finalSaliency;
    }

    private static float ComputeGaussianWeightedAverage(int centerIndex, Vector3[] worldPositions, Dictionary<int, float> curvatureMap, float sigma)
    {
        float weightedSum = 0f;
        float weightTotal = 0f;
        float radius = 2f * sigma;

        for (int i = 0; i < worldPositions.Length; i++)
        {
            if (i == centerIndex) continue;
            float distSqr = (worldPositions[i] - worldPositions[centerIndex]).sqrMagnitude;
            if (distSqr > radius * radius) continue;

            float weight = Mathf.Exp(-distSqr / (2 * sigma * sigma));
            weightTotal += weight;
            weightedSum += curvatureMap[i] * weight;
        }

        return weightTotal > 1e-6f ? weightedSum / weightTotal : 0f;
    }

    private static void ApplyNonlinearSuppression(Dictionary<Vector3, float[]> multiScaleSaliency, out Dictionary<Vector3, float> finalSaliency)
    {
        finalSaliency = new();
        int scaleCount = multiScaleSaliency.Values.First().Length;

        for (int i = 0; i < scaleCount; i++)
        {
            float max = multiScaleSaliency.Values.Max(arr => arr[i]);
            float avgLocalMax = multiScaleSaliency.Values.Select(arr => arr[i]).OrderByDescending(x => x).Take(10).Average();
            float weight = Mathf.Pow(max - avgLocalMax, 2f);

            foreach (var kvp in multiScaleSaliency)
            {
                if (!finalSaliency.ContainsKey(kvp.Key))
                    finalSaliency[kvp.Key] = 0f;
                finalSaliency[kvp.Key] += kvp.Value[i] * weight;
            }
        }
    }

    private static Dictionary<int, float> ComputeMeanCurvatures(MeshFilter meshFilter)
    {
        Mesh mesh = meshFilter.sharedMesh;
        Vector3[] vertices = mesh.vertices;
        Vector3[] worldVertices = vertices.Select(v => meshFilter.transform.TransformPoint(v)).ToArray();
        int[] triangles = mesh.triangles;

        Dictionary<int, List<(int j, float weight)>> cotangentWeights = new();
        Dictionary<int, float> areaSum = new();
        Dictionary<int, float> curvature = new();

        for (int i = 0; i < vertices.Length; i++)
        {
            cotangentWeights[i] = new List<(int, float)>();
            areaSum[i] = 0f;
        }

        for (int i = 0; i < triangles.Length; i += 3)
        {
            int i0 = triangles[i];
            int i1 = triangles[i + 1];
            int i2 = triangles[i + 2];

            Vector3 v0 = worldVertices[i0];
            Vector3 v1 = worldVertices[i1];
            Vector3 v2 = worldVertices[i2];

            float area = Vector3.Cross(v1 - v0, v2 - v0).magnitude / 6f;
            areaSum[i0] += area;
            areaSum[i1] += area;
            areaSum[i2] += area;

            AddCotangent(i0, i1, i2, v0, v1, v2, cotangentWeights);
            AddCotangent(i1, i2, i0, v1, v2, v0, cotangentWeights);
            AddCotangent(i2, i0, i1, v2, v0, v1, cotangentWeights);
        }

        foreach (int i in cotangentWeights.Keys)
        {
            Vector3 sum = Vector3.zero;
            foreach (var (j, weight) in cotangentWeights[i])
            {
                sum += weight * (worldVertices[j] - worldVertices[i]);
            }

            float A = areaSum[i] > 1e-6f ? areaSum[i] : 1f;
            curvature[i] = sum.magnitude / (2f * A);
        }

        return curvature;
    }

    private static void AddCotangent(int i0, int i1, int i2, Vector3 v0, Vector3 v1, Vector3 v2,
                                      Dictionary<int, List<(int j, float weight)>> cotangentWeights)
    {
        Vector3 a = v1 - v0;
        Vector3 b = v2 - v0;
        float cot = Vector3.Dot(a, b) / Vector3.Cross(a, b).magnitude;

        if (!float.IsNaN(cot) && !float.IsInfinity(cot))
        {
            cotangentWeights[i0].Add((i1, cot));
            cotangentWeights[i1].Add((i0, cot));
        }
    }
    //Mesh Saliency 논문의 방식
    private static Dictionary<int, float> ComputeMeanCurvatures_Taubin(MeshFilter meshFilter)
    {
        Mesh mesh = meshFilter.sharedMesh;
        Vector3[] vertices = mesh.vertices;
        Vector3[] worldVertices = vertices.Select(v => meshFilter.transform.TransformPoint(v)).ToArray();
        Dictionary<int, HashSet<int>> adjacency = SaliencyUtils.GetOrBuildAdjacency(mesh);

        Dictionary<int, float> curvature = new();

        for (int i = 0; i < vertices.Length; i++)
        {
            if (!adjacency.TryGetValue(i, out var neighbors) || neighbors.Count == 0)
            {
                curvature[i] = 0f;
                continue;
            }

            Vector3 vi = worldVertices[i];
            Vector3 avgNeighbor = Vector3.zero;

            foreach (int j in neighbors)
            {
                avgNeighbor += worldVertices[j];
            }

            avgNeighbor /= neighbors.Count;
            Vector3 laplacian = avgNeighbor - vi;
            curvature[i] = laplacian.magnitude; // or .sqrMagnitude for speed
        }

        return curvature;
    }
}