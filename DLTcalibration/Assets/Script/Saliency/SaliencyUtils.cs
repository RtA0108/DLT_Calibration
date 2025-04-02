using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class SaliencyUtils
{
    public static List<Vector3> GetVisibleVertices(Camera camera, MeshFilter meshFilter, Dictionary<int, Vector3> vertexPositions, LayerMask visibilityLayerMask)
    {
        List<Vector3> visibleVertices = new List<Vector3>();
        Vector3 camPos = camera.transform.position;
        Dictionary<Vector3, bool> uniquePositions = new Dictionary<Vector3, bool>();

        foreach (var kvp in vertexPositions)
        {
            int vertexIndex = kvp.Key;
            Vector3 vertexWorldPos = meshFilter.transform.TransformPoint(kvp.Value);
            Vector3 roundedVertex = new Vector3(
                Mathf.Round(vertexWorldPos.x * 1000f) / 1000f,
                Mathf.Round(vertexWorldPos.y * 1000f) / 1000f,
                Mathf.Round(vertexWorldPos.z * 1000f) / 1000f
            );

            Vector3 worldNormal = meshFilter.transform.TransformDirection(meshFilter.mesh.normals[vertexIndex]);
            Vector3 toCamera = (camPos - vertexWorldPos).normalized;

            if (Vector3.Dot(worldNormal, toCamera) <= 0) continue;

            Ray ray = new Ray(camPos, (vertexWorldPos - camPos).normalized);
            if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, visibilityLayerMask))
            {
                if (Vector3.Distance(hit.point, vertexWorldPos) < 0.01f)
                {
                    if (!uniquePositions.ContainsKey(roundedVertex))
                    {
                        uniquePositions[roundedVertex] = true;
                        visibleVertices.Add(vertexWorldPos);
                    }
                }
            }
        }

        return visibleVertices;
    }

    public static List<Vector3> FilterSilhouetteVertices(Camera cam, MeshFilter meshFilter, List<Vector3> vertices, float sigma, float dotThreshold = 0.3f, float varianceThreshold = 0.15f)
    {
        List<Vector3> filtered = new List<Vector3>();
        Vector3 cameraPos = cam.transform.position;

        foreach (Vector3 v in vertices)
        {
            Vector3 normal = GetVertexNormal(meshFilter, v).normalized;
            Vector3 toCamera = (cameraPos - v).normalized;
            //cameraDot이 높을수록 정면, 즉 dotThreshold가 높을수록 꼼꼼히 검토하는 것
            float cameraDot = Mathf.Abs(Vector3.Dot(normal, toCamera));

            // 1단계: normal과 카메라 시선이 수직에 가까운가?
            if (cameraDot > dotThreshold)
            {
                filtered.Add(v); //정면을 향하고 있음 -> 실루엣 아님
                continue;
            }

            // 2단계: 주변 normal과의 평균 편차가 작은가?
            List<Vector3> neighbors = GetNeighborsByEuclideanDistance(v, sigma, vertices.ToArray());
            if (neighbors.Count == 0)
            {
                filtered.Add(v); //주변에 없으면 유지
                continue;
            }

            float variance = neighbors.Sum(n => (1f - Vector3.Dot(normal, GetVertexNormal(meshFilter, n).normalized))) / neighbors.Count;

            if (variance < varianceThreshold)
                filtered.Add(v); //곡률이 급하지 않음 -> 유지
            //else
            //{
            // 실루엣 정점으로 판단 → 제거
            //Debug.Log($"[Silhouette Removed] {v}, Dot: {cameraDot:F2}, Var: {variance:F2}");
            //}
        }

        Debug.Log($"[Silhouette Filter] Before: {vertices.Count}, After: {filtered.Count}");
        return filtered;
    }

    public static List<Vector3> SelectHybridDistributedVertices(List<Vector3> candidates, Dictionary<Vector3, float> saliencyMap, float l, int selectionCount, float alpha = 0.5f)
    {
        List<Vector3> selected = new List<Vector3>();

        if (candidates.Count <= selectionCount)
            return new List<Vector3>(candidates);

        // --- Step 1: 가장 높은 엔트로피를 가진 정점으로 첫 정점 선택 ---
        Vector3 first = candidates.OrderByDescending(v => saliencyMap[v]).First();
        selected.Add(first);
        candidates.Remove(first);

        // --- Step 2: Hybrid 분산 선택 ---
        while (selected.Count < selectionCount)
        {
            Vector3 bestVertex = Vector3.zero;
            float bestScore = float.MinValue;

            foreach (var candidate in candidates)
            {
                float minDist = selected.Min(s => Vector3.Distance(candidate, s));
                // 하이브리드 점수 (alpha 조절) -> alpha가 높을수록 saliency에 대한 영향 증가
                float score = alpha * saliencyMap[candidate] + (1 - alpha) * (minDist / l);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestVertex = candidate;
                }
            }

            if (bestVertex != Vector3.zero)
            {
                selected.Add(bestVertex);
                candidates.Remove(bestVertex);
            }
            else break;
        }

        return selected;
    }

    public static void HighlightVertex(Vector3 position, Color color, float scale, bool isSaliencyHighlight, int index = -1)
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.transform.position = position;
        sphere.transform.localScale = Vector3.one * scale;
        if (isSaliencyHighlight)
            sphere.name = $"HighVertex_{index}";

        Renderer r = sphere.GetComponent<Renderer>();
        if (r != null)
        {
            r.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            r.material.color = color;
        }
    }

    private static List<Vector3> GetNeighborsByEuclideanDistance(Vector3 position, float sigma, Vector3[] vertices)
    {
        List<Vector3> neighbors = new List<Vector3>();
        float sigmaSquared = sigma * sigma; // 거리 계산을 제곱 비교로 최적화

        foreach (Vector3 vertex in vertices)
        {
            float distSqr = (position - vertex).sqrMagnitude; // 거리 제곱값 계산
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
}
