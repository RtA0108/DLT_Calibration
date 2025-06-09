using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using MathNet.Numerics.LinearAlgebra;

public class SpectralSaliencyComputer : MonoBehaviour
{
    public MeshFilter meshFilter;
    public Dictionary<int, float> vertexSaliency = new Dictionary<int, float>();

    public void ComputeSaliency()
    {
        Mesh mesh = meshFilter.sharedMesh;
        Vector3[] vertices = mesh.vertices;

        // 1. Build adjacency list (as HashSet)
        Dictionary<int, HashSet<int>> neighbors = SaliencyUtils.BuildAdjacency(mesh);

        // 2. Build geometric Laplacian matrix (distance-weighted)
        var L = BuildGeometricLaplacian(vertices, neighbors);

        // 3. Eigen decomposition
        var evd = L.Evd();
        var eigenVectors = evd.EigenVectors;
        var eigenValues = evd.EigenValues.Select(c => c.Real).ToArray();

        // 4. Log-Laplacian Spectrum
        float[] logSpectrum = eigenValues.Select(lambda => Mathf.Log(Mathf.Abs((float)lambda) + 1e-6f)).ToArray();

        // 5. Local average and deviation (spectral irregularity)
        float[] avgSpectrum = LocalAverage(logSpectrum, 9);
        float[] spectralDeviation = logSpectrum.Zip(avgSpectrum, (l, a) => Mathf.Abs(l - a)).ToArray();

        // 6. Transform back to spatial domain
        int n = spectralDeviation.Length;

        // 6-1. exp(deviation) 미리 계산
        float[] diagonalValues = spectralDeviation.Select(x => Mathf.Exp(x)).ToArray();

        // 6-2. 빈 행렬 만들기
        var R = Matrix<float>.Build.Dense(n, n, 0f);

        // 6-3. 대각 원소만 직접 채우기
        for (int i = 0; i < n; i++)
        {
            R[i, i] = diagonalValues[i];
        }
        var S = eigenVectors * R * eigenVectors.Transpose();

        // 7. Vertex-wise saliency from row sums
        for (int i = 0; i < S.RowCount; i++)
        {
            vertexSaliency[i] = S.Row(i).Sum();
        }
    }

    private Matrix<float> BuildGeometricLaplacian(Vector3[] vertices, Dictionary<int, HashSet<int>> neighbors)
    {
        int n = vertices.Length;
        var W = Matrix<float>.Build.Dense(n, n, 0f);

        // 1. Weight matrix W 구성 (distance-based)
        for (int i = 0; i < n; i++)
        {
            if (!neighbors.ContainsKey(i)) continue;

            foreach (int j in neighbors[i])
            {
                float distSq = (vertices[i] - vertices[j]).sqrMagnitude + 1e-6f;
                W[i, j] = 1f / distSq;
            }
        }

        // 2. Row 정규화 (W의 각 row의 합이 1이 되도록)
        for (int i = 0; i < n; i++)
        {
            float rowSum = W.Row(i).Sum();
            if (rowSum > 0)
            {
                for (int j = 0; j < n; j++)
                    W[i, j] /= rowSum;
            }
        }

        // 3. Diagonal matrix D 구성 (W의 row sum)
        var D = Matrix<float>.Build.Dense(n, n, 0f);
        for (int i = 0; i < n; i++)
            D[i, i] = W.Row(i).Sum();

        // 4. Laplacian L = W - D
        return W - D;
    }

    private float[] LocalAverage(float[] spectrum, int window)
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
