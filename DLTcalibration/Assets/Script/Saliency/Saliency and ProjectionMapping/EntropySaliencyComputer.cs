using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Diagnostics;
public static class EntropySaliencyComputer
{
    public static Dictionary<Vector3, float> Compute(MeshFilter meshFilter, List<Vector3> visibleVertices, float l)
    {
        // Stopwatch 시작
        Stopwatch sw = new Stopwatch();
        sw.Start();

        Dictionary<Vector3, float> entropyMap = new Dictionary<Vector3, float>();
        float[] entropyValues = ComputeVertexEntropy(visibleVertices, l, meshFilter);

        for (int i = 0; i < visibleVertices.Count; i++)
            entropyMap[visibleVertices[i]] = entropyValues[i];
        //Debug.Log($"[EntropySaliencyComputer] EntropyMap Generated: {entropyMap.Count} entries for {visibleVertices.Count} visible vertices.");
        sw.Stop(); // Stopwatch 정지
        UnityEngine.Debug.Log($"[Profile] EntropySaliencyComputer.Compute: {sw.Elapsed.TotalSeconds:F2} seconds");

        return entropyMap;
    }

    //-------------------------------------------------------------------------------------multi sigma ver----------------------------------------------
    //private static float[] ComputeVertexEntropy(List<Vector3> visibleVertices, float l, MeshFilter meshFilter)
    //{
    //    float[] entropyValues = new float[visibleVertices.Count];

    //    List<int> neighborCounts = new List<int>();

    //    Mesh mesh = meshFilter.sharedMesh;
    //    //Dictionary<int, HashSet<int>> adjacency = SaliencyUtils.GetOrBuildAdjacency(mesh);

    //    Vector3[] worldPositions = mesh.vertices.Select(v => meshFilter.transform.TransformPoint(v)).ToArray();
    //    Vector3[] normals = mesh.normals;
    //    float[] sigmaScales = new float[] { 0.07f * l, 0.075f * l, 0.08f * l };

    //    //추가: 위치 → index 리스트 캐시 생성
    //    Dictionary<Vector3, List<int>> positionToIndicesMap = SaliencyUtils.GetOrBuildPositionToIndicesMap(mesh, meshFilter.transform);


    //    for (int i = 0; i < visibleVertices.Count; i++)
    //    {
    //        Vector3 vertex = visibleVertices[i];
    //        int vIndex = SaliencyUtils.FindNearestVertexIndex(vertex, worldPositions);

    //        float aggregatedEntropy = 0f;
    //        float totalWeight = 0f;

    //        //HashSet<int> topologyNeighbors = SaliencyUtils.FindTopologyNeighbors(vIndex, adjacency, 2, mesh, meshFilter.transform);

    //        foreach (float sigma in sigmaScales)
    //        {
    //            //UnityEngine.Debug.Log($"[σ] sigma = {sigma:F5}, l = {l:F5}");

    //            // 추가: 거리 기반 필터링 (sigma 적용)
    //            Vector3 center = worldPositions[vIndex];
    //            //HashSet<int> neighbors = topologyNeighbors
    //            ////.Where(idx => Vector3.Distance(worldPositions[idx], center) <= sigma)
    //            //.Where(idx => (worldPositions[idx] - center).sqrMagnitude <= sigma * sigma)
    //            //.ToHashSet();
    //            HashSet<int> neighbors = SaliencyUtils.FindEuclideanNeighbors(worldPositions, vIndex, sigma);

    //            neighborCounts.Add(neighbors.Count);
    //            if (neighbors.Count == 0) continue;

    //            Dictionary<Vector3Int, int> normalHistogram = new();

    //            foreach (int ni in neighbors)
    //            {
    //                //Vector3 normal = normals[ni];

    //                Vector3 neighborWorldPos = worldPositions[ni];

    //                if (!positionToIndicesMap.TryGetValue(neighborWorldPos, out var indices)) continue;
    //                // index가 하나만 있으면 바로 normal을 사용하고, 여러 개면 평균을 구함
    //                Vector3 avgNormal = (indices.Count == 1) ? normals[indices[0]] : indices.Aggregate(Vector3.zero, (acc, idx) => acc + normals[idx]).normalized;
    //                //Vector3 avgNormal = Vector3.zero;
    //                //foreach (int idx in indices)
    //                    //avgNormal += normals[idx];

    //                //avgNormal.Normalize();
    //                // 평균 normal에 TransformDirection 적용 (world space 보정)
    //                Vector3 transformed = meshFilter.transform.TransformDirection(avgNormal);

    //                Vector3Int quantized = SphericalQuantizeNormal(transformed, 10); // 10으로 할지 20으로 할지 미정
    //                if (!normalHistogram.TryAdd(quantized, 1))
    //                    normalHistogram[quantized]++;
    //            }

    //            if (normalHistogram.Count == 0) continue;

    //            float entropy = 0f;
    //            int totalNormals = neighbors.Count;

    //            foreach (var count in normalHistogram.Values)
    //            {
    //                float p = (float)count / (totalNormals + 1e-6f);
    //                if (p > 0f && !float.IsNaN(p))
    //                    entropy -= p * Mathf.Log(p + 1e-6f);
    //            }

    //            if (float.IsNaN(entropy) || float.IsInfinity(entropy)) entropy = 0f;

    //            float maxEntropy = Mathf.Log(normalHistogram.Keys.Count + 1e-6f);
    //            float normalized = entropy / (maxEntropy + 1e-6f);
    //            if (float.IsNaN(normalized) || float.IsInfinity(normalized)) normalized = 0f;

    //            normalized = Mathf.Pow(normalized, 2.0f);
    //            float saliency = Mathf.Clamp01(normalized);
    //            float weight = 1.0f / sigma;

    //            aggregatedEntropy += saliency * weight;
    //            totalWeight += weight;
    //        }

    //        entropyValues[i] = (totalWeight > 0f) ? aggregatedEntropy / totalWeight : 1e-3f;
    //    }
    //    if (neighborCounts.Count > 0)
    //    {
    //        int min = neighborCounts.Min();
    //        int max = neighborCounts.Max();
    //        float avg = (float)neighborCounts.Average();
    //        int under10 = neighborCounts.Count(n => n < 10);
    //        int under5 = neighborCounts.Count(n => n < 5);

    //        UnityEngine.Debug.Log($"[Entropy Neighbor Stat] Total={neighborCounts.Count}, Min={min}, Max={max}, Avg={avg:F2}, <10={under10}, <5={under5}");
    //    }
    //    return entropyValues;
    //}
    //------------------------------------------------------------------------------------------------------------------------------------------------------------
    private static float[] ComputeVertexEntropy(List<Vector3> visibleVertices, float l, MeshFilter meshFilter)
    {
        float[] entropyValues = new float[visibleVertices.Count];
        List<int> neighborCounts = new List<int>();

        Mesh mesh = meshFilter.sharedMesh;
        Vector3[] worldPositions = mesh.vertices.Select(v => meshFilter.transform.TransformPoint(v)).ToArray();
        Vector3[] normals = mesh.normals;
        float sigma = 0.05f * l;

        //sigma, Threshold 보정 코드(parameter)

        //float initialSigma = 0.07f * l;
        //float sigmaMin = 0.03f * l;
        //float sigmaMax = 0.15f * l;

        //// 메시 크기에 따라 적절한 sigma를 선택하기 위한 후보값들
        //float[] sigmaCandidates = new float[]
        //{
        //0.04f * l,
        //0.05f * l,
        //0.06f * l,
        //0.07f * l,
        //0.08f * l,
        //0.09f * l
        //};

        // 메시 크기에 따라 neighbor threshold를 자동 설정
        //int vertexCount = mesh.vertexCount;
        //int minNeighborThreshold = Mathf.Max(10, vertexCount / 1000); // 예: 16000 → 16
        //int maxNeighborThreshold = Mathf.Clamp(vertexCount / 100, 50, 600); // 예: 16000 → 160
        //UnityEngine.Debug.Log($"Min: {minNeighborThreshold}, Max: {maxNeighborThreshold}");
        //----------

        Dictionary<Vector3, List<int>> positionToIndicesMap = SaliencyUtils.GetOrBuildPositionToIndicesMap(mesh, meshFilter.transform);

        // [1] 가장 적절한 sigma 하나를 먼저 선택
        //float selectedSigma = sigmaCandidates[0];
        //int bestCoverage = 0;
        //foreach (float s in sigmaCandidates)
        //{
        //    int validCount = 0;
        //    foreach (Vector3 vertex in visibleVertices)
        //    {
        //        int index = SaliencyUtils.FindNearestVertexIndex(vertex, worldPositions);
        //        var neighbors = SaliencyUtils.FindEuclideanNeighbors(worldPositions, index, s);
        //        if (neighbors.Count >= minNeighborThreshold && neighbors.Count <= maxNeighborThreshold)
        //            validCount++;
        //    }
        //    if (validCount > bestCoverage)
        //    {
        //        bestCoverage = validCount;
        //        selectedSigma = s;
        //    }
        //}
        //float sigma = selectedSigma;
        //UnityEngine.Debug.Log($"[Selected Sigma] σ = {selectedSigma:F5} (Coverage: {bestCoverage}/{visibleVertices.Count})");


        for (int i = 0; i < visibleVertices.Count; i++)
        {
            Vector3 vertex = visibleVertices[i];
            int vIndex = SaliencyUtils.FindNearestVertexIndex(vertex, worldPositions);

            //sigma 보정을 안사용하면 지우기
            //float sigma = initialSigma;

            HashSet<int> neighbors = SaliencyUtils.FindEuclideanNeighbors(worldPositions, vIndex, sigma);


            // adaptive sigma 조정
            //if (neighborCount < minNeighborThreshold && sigma < sigmaMax)
            //{
            //    sigma *= 1.5f;
            //    neighbors = SaliencyUtils.FindEuclideanNeighbors(worldPositions, vIndex, sigma);
            //    neighborCount = neighbors.Count;
            //}
            //else if (neighborCount > maxNeighborThreshold && sigma > sigmaMin)
            //{
            //    sigma *= 0.7f;
            //    neighbors = SaliencyUtils.FindEuclideanNeighbors(worldPositions, vIndex, sigma);
            //    neighborCount = neighbors.Count;
            //}

            //int attempt = 0;
            //while ((neighbors.Count < minNeighborThreshold || neighbors.Count > maxNeighborThreshold) && attempt < 10)
            //{
            //    if (neighbors.Count < minNeighborThreshold && sigma < sigmaMax)
            //        sigma *= 1.5f;
            //    else if (neighbors.Count > maxNeighborThreshold && sigma > sigmaMin)
            //        sigma *= 0.7f;

            //    neighbors = SaliencyUtils.FindEuclideanNeighbors(worldPositions, vIndex, sigma);
            //    attempt++;
            //}

            int neighborCount = neighbors.Count;
            neighborCounts.Add(neighborCount);
            if (neighbors.Count == 0)
            {
                entropyValues[i] = 1e-3f;
                continue;
            }

            Dictionary<Vector3Int, int> normalHistogram = new();

            foreach (int ni in neighbors)
            {
                Vector3 neighborWorldPos = worldPositions[ni];
                if (!positionToIndicesMap.TryGetValue(neighborWorldPos, out var indices)) continue;

                Vector3 avgNormal = (indices.Count == 1) ? normals[indices[0]] : indices.Aggregate(Vector3.zero, (acc, idx) => acc + normals[idx]).normalized;
                Vector3 transformed = meshFilter.transform.TransformDirection(avgNormal);

                Vector3Int quantized = SphericalQuantizeNormal(transformed, 10); // 정규화된 분포가 과하지 않게
                if (!normalHistogram.TryAdd(quantized, 1))
                    normalHistogram[quantized]++;
            }

            if (normalHistogram.Count == 0)
            {
                entropyValues[i] = 1e-3f;
                continue;
            }

            float entropy = 0f;
            int totalNormals = neighbors.Count;

            foreach (var count in normalHistogram.Values)
            {
                float p = (float)count / (totalNormals + 1e-6f);
                if (p > 0f && !float.IsNaN(p))
                    entropy -= p * Mathf.Log(p + 1e-6f);
            }

            if (float.IsNaN(entropy) || float.IsInfinity(entropy)) entropy = 0f;

            float maxEntropy = Mathf.Log(normalHistogram.Keys.Count + 1e-6f);
            float normalized = entropy / (maxEntropy + 1e-6f);
            if (float.IsNaN(normalized) || float.IsInfinity(normalized)) normalized = 0f;

            normalized = Mathf.Pow(normalized, 2.0f);
            float saliency = Mathf.Clamp01(normalized);

            entropyValues[i] = saliency;
        }

        if (neighborCounts.Count > 0)
        {
            int min = neighborCounts.Min();
            int max = neighborCounts.Max();
            float avg = (float)neighborCounts.Average();
            int under10 = neighborCounts.Count(n => n < 10);
            int under5 = neighborCounts.Count(n => n < 5);

            UnityEngine.Debug.Log($"[Entropy Neighbor Stat] Total={neighborCounts.Count}, Min={min}, Max={max}, Avg={avg:F2}, <10={under10}, <5={under5}");
        }

        return entropyValues;
    }
    public static Dictionary<Vector3, float> NormalizeSaliencyMap(Dictionary<Vector3, float> saliencyMap)
    {
        Dictionary<Vector3, float> normalized = new();
        float min = saliencyMap.Values.Min();
        float max = saliencyMap.Values.Max();
        foreach (var kv in saliencyMap)
        {
            float t = Mathf.Clamp01((kv.Value - min) / (max - min));
            normalized[kv.Key] = t;
        }
        return normalized;
    }

    public static Dictionary<Vector3, float> GroupSaliencyByRoundedPosition(Dictionary<Vector3, float> saliencyMap, int precision = 1000)
    {
        return saliencyMap
            .GroupBy(kv => new Vector3(
                Mathf.Round(kv.Key.x * precision) / precision,
                Mathf.Round(kv.Key.y * precision) / precision,
                Mathf.Round(kv.Key.z * precision) / precision))
            .ToDictionary(g => g.Key, g => g.First().Value);
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
                UnityEngine.Debug.LogWarning($"[Filtered Vertex Missing in EntropyMap] Pos={v}");
            }
        }

        UnityEngine.Debug.Log($"[Entropy Map Match Check] Matched: {matched}, Unmatched: {unmatched}, Total Filtered: {filteredVertices.Count}");
    }

    private static Dictionary<Vector3, Vector3> BuildNormalCache(MeshFilter meshFilter)
    {
        Vector3[] positions = meshFilter.mesh.vertices;
        Vector3[] normals = meshFilter.mesh.normals;
        Dictionary<Vector3, Vector3> normalCache = new();

        for (int i = 0; i < positions.Length; i++)
        {
            Vector3 worldPos = meshFilter.transform.TransformPoint(positions[i]);
            normalCache[worldPos] = normals[i];
        }
        return normalCache;
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
    // 개선된 Spherical Quantization 방식
    private static Vector3Int SphericalQuantizeNormal(Vector3 normal, int resolution)
    {
        normal.Normalize();
        float theta = Mathf.Acos(Mathf.Clamp(normal.y, -1f, 1f)); // elevation
        float phi = Mathf.Atan2(normal.z, normal.x);              // azimuth
        if (phi < 0f) phi += 2f * Mathf.PI;

        int thetaIndex = Mathf.FloorToInt(theta / Mathf.PI * resolution);
        int phiIndex = Mathf.FloorToInt(phi / (2f * Mathf.PI) * resolution);

        return new Vector3Int(thetaIndex, phiIndex, 0);
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