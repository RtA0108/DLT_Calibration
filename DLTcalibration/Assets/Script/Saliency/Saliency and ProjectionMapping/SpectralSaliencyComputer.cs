using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using MathNet.Numerics.LinearAlgebra;

public static class SpectralSaliencyComputer
{
    public static Dictionary<Vector3, float> Compute(MeshFilter meshFilter, List<Vector3> filtered, float l)
    {
        Mesh mesh = meshFilter.sharedMesh;
        Vector3[] localVertices = mesh.vertices;
        Vector3[] worldVertices = localVertices.Select(v => meshFilter.transform.TransformPoint(v)).ToArray();

        // 1. Build adjacency list
        Dictionary<int, HashSet<int>> neighbors = SaliencyUtils.BuildAdjacency(mesh);

        // 2. Build geometric Laplacian
        var L = BuildGeometricLaplacian(localVertices, neighbors);

        // 3. Eigen decomposition
        var evd = L.Evd();
        var eigenVectors = evd.EigenVectors;
        var eigenValues = evd.EigenValues.Select(c => c.Real).ToArray();

        // 4. Multi-scale saliency computation (Section 3)
        int[] scales = new int[] { 3, 5, 7, 9, 11 };
        int n = eigenValues.Length;
        float[][] multiScaleDeviations = new float[scales.Length][];

        for (int s = 0; s < scales.Length; s++)
        {
            float[] logSpectrum = eigenValues.Select(lambda => Mathf.Log(Mathf.Abs((float)lambda) + 1e-6f)).ToArray();
            float[] avgSpectrum = LocalAverage(logSpectrum, scales[s]);
            float[] spectralDeviation = logSpectrum.Zip(avgSpectrum, (lval, avg) => Mathf.Abs(lval - avg)).ToArray();
            multiScaleDeviations[s] = spectralDeviation;
        }

        // 5. Combine multiscale saliency via sum of exp deviations
        float[] combinedDeviation = new float[n];
        for (int i = 0; i < n; i++)
        {
            float sum = 0f;
            for (int s = 0; s < scales.Length; s++)
                sum += Mathf.Exp(multiScaleDeviations[s][i]);
            combinedDeviation[i] = Mathf.Log(sum + 1e-6f);
        }

        // 6. Back project to spatial domain
        var R = Matrix<float>.Build.Dense(n, n, 0f);
        for (int i = 0; i < n; i++)
            R[i, i] = combinedDeviation[i];
        var S = eigenVectors * R * eigenVectors.Transpose();

        // 7. Vertex-wise saliency from row sums
        Dictionary<Vector3, float> saliencyMap = new();
        for (int i = 0; i < S.RowCount; i++)
            saliencyMap[worldVertices[i]] = S.Row(i).Sum();

        // 8. 필터링된 vertex만 추림
        Dictionary<Vector3, float> filteredMap = new();
        foreach (var v in filtered)
        {
            if (saliencyMap.TryGetValue(v, out float s))
                filteredMap[v] = s;
        }

        return filteredMap;
    }

    private static Matrix<float> BuildGeometricLaplacian(Vector3[] vertices, Dictionary<int, HashSet<int>> neighbors)
    {
        int n = vertices.Length;
        var W = Matrix<float>.Build.Dense(n, n, 0f);

        for (int i = 0; i < n; i++)
        {
            if (!neighbors.ContainsKey(i)) continue;
            foreach (int j in neighbors[i])
            {
                float distSq = (vertices[i] - vertices[j]).sqrMagnitude + 1e-6f;
                W[i, j] = 1f / distSq;
            }
        }

        for (int i = 0; i < n; i++)
        {
            float rowSum = W.Row(i).Sum();
            if (rowSum > 0)
            {
                for (int j = 0; j < n; j++)
                    W[i, j] /= rowSum;
            }
        }

        var D = Matrix<float>.Build.Dense(n, n, 0f);
        for (int i = 0; i < n; i++)
            D[i, i] = W.Row(i).Sum();

        return W - D;
    }

    private static float[] LocalAverage(float[] spectrum, int window)
    {
        int n = spectrum.Length;
        float[] avg = new float[n];
        int half = window / 2;
        for (int i = 0; i < n; i++)
        {
            int start = Mathf.Max(0, i - half);
            int end = Mathf.Min(n - 1, i + half);
            float sum = 0;
            for (int j = start; j <= end; j++) sum += spectrum[j];
            avg[i] = sum / (end - start + 1);
        }
        return avg;
    }
}
