using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class MeshSaliencyComputer
{
    public static Dictionary<Vector3, float> Compute(MeshFilter meshFilter, List<Vector3> vertices, float l)
    {
        // Parameters
        float[] sigmaScales = new float[] { 2f * 0.003f * l, 3f * 0.003f * l, 4f * 0.003f * l, 5f * 0.003f * l, 6f * 0.003f * l };

        // Step 1: Compute Mean Curvature for all vertices
        Dictionary<Vector3, float> curvatureMap = ComputeMeanCurvature(meshFilter, vertices);
        // Step 2: Multi-Scale Center-Surround Saliency
        Dictionary<Vector3, float[]> multiScaleSaliency = new Dictionary<Vector3, float[]>();

        foreach (var v in vertices)
        {
            multiScaleSaliency[v] = new float[sigmaScales.Length];

            for (int i = 0; i < sigmaScales.Length; i++)
            {
                float sigma = sigmaScales[i];
                float fine = GaussianWeightedMean(curvatureMap, v, vertices, sigma);
                float coarse = GaussianWeightedMean(curvatureMap, v, vertices, 2f * sigma);
                multiScaleSaliency[v][i] = Mathf.Abs(fine - coarse);
            }
        }

        // Step 3: Non-linear Suppression (Itti Style)
        float[] Mi = new float[sigmaScales.Length];
        float[] mBar = new float[sigmaScales.Length];
        for (int i = 0; i < sigmaScales.Length; i++)
        {
            var scaleSaliency = multiScaleSaliency.Values.Select(s => s[i]).ToList();
            Mi[i] = scaleSaliency.Max();
            mBar[i] = scaleSaliency.Where(s => s != Mi[i]).DefaultIfEmpty(0f).Average();
        }

        // Step 4: Aggregate Saliency Map
        Dictionary<Vector3, float> saliencyMap = new Dictionary<Vector3, float>();
        foreach (var v in vertices)
        {
            float S = 0f;
            for (int i = 0; i < sigmaScales.Length; i++)
                S += multiScaleSaliency[v][i] * Mathf.Pow(Mi[i] - mBar[i], 2);

            saliencyMap[v] = S;
        }

        return saliencyMap;
    }

    // -------------------------
    // Mean Curvature Estimation (Simple Version)
    private static Dictionary<Vector3, float> ComputeMeanCurvature(MeshFilter meshFilter, List<Vector3> vertices)
    {
        Dictionary<Vector3, float> curvatureMap = new Dictionary<Vector3, float>();

        foreach (var v in vertices)
        {
            Vector3[] neighbors = GetNeighborsByEuclideanDistance(v, 0.05f * meshFilter.mesh.bounds.size.magnitude, vertices.ToArray()).ToArray();
            if (neighbors.Length < 1)
            {
                curvatureMap[v] = 0f;
                continue;
            }

            Vector3 laplacian = neighbors.Aggregate(Vector3.zero, (acc, n) => acc + (n - v)) / neighbors.Length;
            curvatureMap[v] = laplacian.magnitude;
        }

        return curvatureMap;
    }

    // -------------------------
    // Gaussian Weighted Mean
    private static float GaussianWeightedMean(Dictionary<Vector3, float> map, Vector3 v, List<Vector3> vertices, float sigma)
    {
        float numerator = 0f;
        float denominator = 0f;
        float sigma2 = 2 * sigma * sigma;

        foreach (var n in vertices)
        {
            float dist2 = (v - n).sqrMagnitude;
            if (dist2 > sigma * sigma * 4f) continue;

            float w = Mathf.Exp(-dist2 / sigma2);
            numerator += w * map[n];
            denominator += w;
        }

        return denominator > 0f ? numerator / denominator : 0f;
    }

    // -------------------------
    // [New] Local Private Neighbor Search
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
}