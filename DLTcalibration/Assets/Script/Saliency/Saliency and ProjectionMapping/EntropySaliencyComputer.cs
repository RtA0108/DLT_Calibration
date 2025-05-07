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
        //Debug.Log($"[EntropySaliencyComputer] EntropyMap Generated: {entropyMap.Count} entries for {visibleVertices.Count} visible vertices.");
        return entropyMap;
    }

    private static float[] ComputeVertexEntropy(List<Vector3> visibleVertices, float l, MeshFilter meshFilter)
    {
        float[] entropyValues = new float[visibleVertices.Count];
        Vector3[] allMeshVertices = SaliencyUtils.GetUniqueWorldVertices(meshFilter);
        Vector3[] vertexArray = visibleVertices.ToArray();
        
        float[] sigmaScales = new float[] { 0.07f * l, 0.075f * l, 0.08f * l };
        float sigma_max = sigmaScales.Max();
        
        for (int i = 0; i < visibleVertices.Count; i++)
        {
            List<Vector3> allNeighbors = GetNeighborsByEuclideanDistance(visibleVertices[i], sigma_max, allMeshVertices);

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
                //normalHistogram.Keys.Count
                float maxEntropy = Mathf.Log(neighbors.Count + 1e-6f);
                float normalizedEntropy = entropy / maxEntropy;
                normalizedEntropy = Mathf.Pow(normalizedEntropy, 2f);
                float weight = 1.0f / sigma;
                aggregatedEntropy += normalizedEntropy * weight;
                totalWeight += weight;
            }

            entropyValues[i] = aggregatedEntropy / totalWeight;
        }

        return entropyValues;
    }
    //0425 추가된 함수
    public static Dictionary<Vector3, float> ComputeAtSigma(MeshFilter meshFilter, List<Vector3> visibleVertices, float sigma)
    {
        Dictionary<Vector3, float> entropyMap = new Dictionary<Vector3, float>();
        Vector3[] allMeshVertices = SaliencyUtils.GetUniqueWorldVertices(meshFilter);
        //Vector3[] vertexArray = visibleVertices.ToArray(); -> 이건 뭐지?

        // neighbor 통계용 변수들
        int zeroNeighborCount = 0;
        int oneNeighborCount = 0;
        int twoOrLessNeighborCount = 0;
        List<int> allNeighborCounts = new List<int>();

        for (int i = 0; i < visibleVertices.Count; i++)
        {
            var neighbors = GetNeighborsByEuclideanDistance(visibleVertices[i], sigma, allMeshVertices);
            int nCount = neighbors.Count;
            allNeighborCounts.Add(nCount);
            if (nCount == 0) { zeroNeighborCount++; continue; }
            if (nCount == 1) oneNeighborCount++;
            if (nCount <= 2) twoOrLessNeighborCount++;

            Dictionary<Vector3Int, int> normalHistogram = new Dictionary<Vector3Int, int>();

            foreach (var neighbor in neighbors)
            {
                Vector3 normal = GetVertexNormal(meshFilter, neighbor);
                Vector3Int quantized = QuantizeNormal(normal, 100);

                if (!normalHistogram.ContainsKey(quantized))
                    normalHistogram[quantized] = 0;

                normalHistogram[quantized]++;
            }

            float entropy = 0f;
            int total = neighbors.Count;

            foreach (var count in normalHistogram.Values)
            {
                float p = (float)count / (total + 1e-6f);
                entropy -= p * Mathf.Log(p + 1e-6f);
            }

            float maxEntropy = Mathf.Log(normalHistogram.Keys.Count + 1e-6f);
            float normalized = entropy / (maxEntropy + 1e-6f);
            normalized = Mathf.Clamp01(normalized);

            entropyMap[visibleVertices[i]] = normalized;
        }
        // 디버그 로그
        Debug.Log($"[Entropy Debug σ={sigma:F4}] Total: {visibleVertices.Count}, Zero: {zeroNeighborCount}, One: {oneNeighborCount}, ≤2: {twoOrLessNeighborCount}");
        if (allNeighborCounts.Count > 0)
        {
            float avg = (float)allNeighborCounts.Average();
            Debug.Log($"[Entropy Debug σ={sigma:F4}] 평균 neighbor 수: {avg:F2}, 최대: {allNeighborCounts.Max()}, 최소: {allNeighborCounts.Min()}");
        }

        return entropyMap;
    }


    //디버깅용 함수 (다른 mesh를 넣었는데 빨간점이 안나옴.)
    public static void CheckFilteredVerticesMatch(Dictionary<Vector3, float> entropyMap, List<Vector3> filteredVertices)
    {
        int matched = 0;
        int unmatched = 0;

        foreach (var v in filteredVertices)
        {
            if (entropyMap.ContainsKey(v))
            {
                matched++;
            }
            else
            {
                unmatched++;
                Debug.LogWarning($"[Filtered Vertex Missing in EntropyMap] Pos={v}");
            }
        }

        Debug.Log($"[Entropy Map Match Check] Matched: {matched}, Unmatched: {unmatched}, Total Filtered: {filteredVertices.Count}");
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