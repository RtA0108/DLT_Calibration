using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class SaliencyUtils
{
    //Occlusion Culling
    public static List<Vector3> GetVisibleVertices(
    Camera camera, MeshFilter meshFilter, Dictionary<int, Vector3> vertexPositions, LayerMask visibilityLayerMask)
    {
        List<Vector3> visibleVertices = new List<Vector3>();
        Vector3 camPos = camera.transform.position;
        Dictionary<Vector3, bool> uniquePositions = new Dictionary<Vector3, bool>();

        float dotThreshold = 0.7f; // ← 더 정면을 향한 정점만 raycast 시도
        float hitTolerance = 0.005f; // 거리 기반 (너무 뒤에 있어서 잘 안보이는 vertex 제외)

        foreach (var kvp in vertexPositions)
        {
            int vertexIndex = kvp.Key;
            Vector3 vertexWorldPos = meshFilter.transform.TransformPoint(kvp.Value);

            // 중복 제거용 좌표 정규화
            Vector3 roundedVertex = new Vector3(
                Mathf.Round(vertexWorldPos.x * 1000f) / 1000f,
                Mathf.Round(vertexWorldPos.y * 1000f) / 1000f,
                Mathf.Round(vertexWorldPos.z * 1000f) / 1000f
            );

            Vector3 worldNormal = meshFilter.transform.TransformDirection(meshFilter.mesh.normals[vertexIndex]);
            Vector3 toCamera = (camPos - vertexWorldPos).normalized;

            if (Vector3.Dot(worldNormal, toCamera) <= dotThreshold) continue;

            Ray ray = new Ray(camPos, (vertexWorldPos - camPos).normalized);
            //Mathf.Infinity를 고정된 수치로 변경할지 고민
            if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, visibilityLayerMask))
            {
                 if (Vector3.Distance(hit.point, vertexWorldPos) < hitTolerance)
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
    //필터 정리 (없앨 가능성 많음)
    public static List<Vector3> FilterVerticesForCalibration(
        Camera cam, MeshFilter meshFilter, List<Vector3> vertices,
        float viewportEdgeMarginRatio = 0.1f // 메시 화면 투영 기준 가장자리 비율
    )
    {
        List<Vector3> filtered = new List<Vector3>();
        Vector3 cameraPos = cam.transform.position;
        Vector3 cameraRight = cam.transform.right;

        //float minViewDot = 0.2f; - 1에서 쓰지만, 현재 무의미

        int total = vertices.Count;
        //int edgeRemoved = 0,  userFacingRemoved = 0;//viewDotRemoved = 0,

        // 중복 제거된 전체 mesh vertex
        Vector3[] allMeshVertices = GetUniqueWorldVertices(meshFilter);

        // === [0] Viewport Box Edge Filter ===
        //화면 좌표계로 변환
        List<Vector3> vpPositions = new List<Vector3>();
        foreach (var v in vertices)
        {
            Vector3 vp = cam.WorldToViewportPoint(v);
            vpPositions.Add(vp); 
        }

        //if (vpPositions.Count == 0)
        //{
        //    Debug.LogWarning("[Calibration Filter] No visible vertex in Viewport.");
        //    return new List<Vector3>();
        //}

        //mesh가 화면상에 차지하는 box 찾기
        float vpMinX = vpPositions.Min(vp => vp.x);
        float vpMaxX = vpPositions.Max(vp => vp.x);
        float vpMinY = vpPositions.Min(vp => vp.y);
        float vpMaxY = vpPositions.Max(vp => vp.y);
        //box의 Margin 계산
        float marginX = (vpMaxX - vpMinX) * viewportEdgeMarginRatio;
        float marginY = (vpMaxY - vpMinY) * viewportEdgeMarginRatio;

        List<Vector3> prefiltered = new List<Vector3>();
        foreach (var v in vertices)
        {
            Vector3 vp = cam.WorldToViewportPoint(v);
            if (vp.z < 0f) continue;

            bool isEdge =
                vp.x < vpMinX + marginX || vp.x > vpMaxX - marginX ||
                vp.y < vpMinY + marginY || vp.y > vpMaxY - marginY;

            if (isEdge)
            {
                //edgeRemoved++;
                //HighlightVertex(v, new Color(1f, 0.4f, 1f), 6f, false); // 핑크색
                continue;
            }

            prefiltered.Add(v);
        }

        foreach (Vector3 v in prefiltered)
        {
            Vector3 worldNormal = meshFilter.transform.TransformDirection(GetVertexNormal(meshFilter, v)).normalized;
            Vector3 toCamera = (cameraPos - v).normalized;

            //float sideViewAmount = Mathf.Abs(Vector3.Dot(toCamera, cameraRight));

            // === [1] ViewDot Filter -> 현재는 occlusion에서 조건이 강력하여 큰 의미 없음.
            //float dot = Vector3.Dot(worldNormal, toCamera);
            //if (dot < minViewDot)
            //{
            //    viewDotRemoved++;
            //    //Debug.Log($"[ViewDot Removed] Dot={dot:F3}, Pos={v}");

            //    continue;
            //}

            // === [2] UserNormal (시점 무관)
            //카메라 공간에서 normal vector 변환
            Vector3 camSpaceNormal = cam.transform.InverseTransformDirection(worldNormal);
            //float minYNorm = -0.5f;
            //float maxYNorm = 1.0f;
            //float userNormalXMaxAbs = 0.9f;

            //if (camSpaceNormal.y < minYNorm || camSpaceNormal.y > maxYNorm || Mathf.Abs(camSpaceNormal.x) > userNormalXMaxAbs)
            // 카메라 좌표계 기준으로 재판단
            if (Mathf.Abs(camSpaceNormal.y) > 0.9f || Mathf.Abs(camSpaceNormal.x) > 0.9f)
            {
                //userFacingRemoved++;

                continue;
            }


            filtered.Add(v);
        }


        return filtered;
    }
    // 분산 + saliency score 비율 변경하며 vertex 추천
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
    //중복 없애기 (수정 필요성? -> 추후 확인 예정)
    public static Vector3[] GetUniqueWorldVertices(MeshFilter meshFilter, int precision = 1000)
    {
        return meshFilter.mesh.vertices
            .Select(v => meshFilter.transform.TransformPoint(v))
            .GroupBy(v => new Vector3(
                Mathf.Round(v.x * precision) / precision,
                Mathf.Round(v.y * precision) / precision,
                Mathf.Round(v.z * precision) / precision
            ))
            .Select(g => g.First())
            .ToArray();
    }
    //기존 이웃 탐지 방식 -> 거리 기반이라 걍 쓰레기
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
    // 이건 어디에 사용중?
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

    //----------------------------------neighbor 탐색 수정 중-----------------------------------------------

    /// <summary>
    /// Topology 기반의 이웃 탐색 (depth 제한)
    /// </summary>
    public static HashSet<int> FindTopologyNeighbors(int startIndex, Dictionary<int, HashSet<int>> adjacency, int maxDepth)
    {
        Queue<(int index, int depth)> queue = new();
        HashSet<int> visited = new() { startIndex };
        queue.Enqueue((startIndex, 0));

        while (queue.Count > 0)
        {
            var (current, depth) = queue.Dequeue();
            if (depth >= maxDepth) continue;

            if (adjacency.TryGetValue(current, out var neighbors))
            {
                foreach (int neighbor in neighbors)
                {
                    if (visited.Add(neighbor))
                        queue.Enqueue((neighbor, depth + 1));
                }
            }
        }

        visited.Remove(startIndex);
        return visited;
    }

    /// <summary>
    /// Euclidean 거리 기반 이웃 탐색 (반경 내 정점 반환)
    /// </summary>
    public static HashSet<int> FindEuclideanNeighbors(Vector3[] worldPositions, int centerIndex, float radius)
    {
        HashSet<int> neighbors = new();
        Vector3 center = worldPositions[centerIndex];
        float rSqr = radius * radius;

        for (int i = 0; i < worldPositions.Length; i++)
        {
            if (i == centerIndex) continue;
            if ((worldPositions[i] - center).sqrMagnitude <= rSqr)
                neighbors.Add(i);
        }
        return neighbors;
    }

    /// <summary>
    /// Geodesic 기반 이웃 탐색 (Dijkstra 방식, 거리 제한)
    /// </summary>
    public static HashSet<int> FindGeodesicNeighbors(int startIndex, Mesh mesh, Transform transform, float maxDistance)
    {
        Vector3[] vertices = mesh.vertices;
        Dictionary<int, HashSet<int>> adjacency = BuildAdjacency(mesh);
        Vector3[] worldPositions = vertices.Select(v => transform.TransformPoint(v)).ToArray();

        HashSet<int> result = new();
        Dictionary<int, float> dist = new() { [startIndex] = 0f };
        PriorityQueue<int> queue = new();
        queue.Enqueue(startIndex, 0f);

        while (queue.Count > 0)
        {
            int current = queue.Dequeue();
            float currentDist = dist[current];

            if (current != startIndex)
                result.Add(current);

            if (adjacency.TryGetValue(current, out var neighbors))
            {
                foreach (int neighbor in neighbors)
                {
                    float edgeLength = (worldPositions[current] - worldPositions[neighbor]).magnitude;
                    float newDist = currentDist + edgeLength;

                    if (newDist <= maxDistance && (!dist.ContainsKey(neighbor) || newDist < dist[neighbor]))
                    {
                        dist[neighbor] = newDist;
                        queue.Enqueue(neighbor, newDist);
                    }
                }
            }
        }

        return result;
    }

    // 기존 adjacency 생성 함수 그대로 유지
    public static Dictionary<int, HashSet<int>> BuildAdjacency(Mesh mesh)
    {
        var adjacency = new Dictionary<int, HashSet<int>>();
        int[] triangles = mesh.triangles;

        for (int i = 0; i < triangles.Length; i += 3)
        {
            int v0 = triangles[i];
            int v1 = triangles[i + 1];
            int v2 = triangles[i + 2];
            AddEdge(adjacency, v0, v1);
            AddEdge(adjacency, v1, v2);
            AddEdge(adjacency, v2, v0);
        }
        return adjacency;
    }

    private static void AddEdge(Dictionary<int, HashSet<int>> adj, int a, int b)
    {
        if (!adj.ContainsKey(a)) adj[a] = new HashSet<int>();
        if (!adj.ContainsKey(b)) adj[b] = new HashSet<int>();
        adj[a].Add(b);
        adj[b].Add(a);
    }




    //기존에 수정중이던 neighbor 탐색 방법들 (사용 여부 확인 예정)

    private static Dictionary<Mesh, Dictionary<int, HashSet<int>>> _adjacencyCache = new();
    private static Dictionary<Mesh, Dictionary<Vector3, List<int>>> _positionMapCache = new();

    public static Dictionary<int, HashSet<int>> GetOrBuildAdjacency(Mesh mesh)
    {
        if (_adjacencyCache.TryGetValue(mesh, out var cached))
            return cached;

        var built = BuildAdjacency(mesh);
        _adjacencyCache[mesh] = built;
        return built;
    }

    //public static Dictionary<int, HashSet<int>> BuildAdjacency(Mesh mesh)
    //{
    //    var adjacency = new Dictionary<int, HashSet<int>>();
    //    int[] triangles = mesh.triangles;

    //    for (int i = 0; i < triangles.Length; i += 3)
    //    {
    //        int v0 = triangles[i];
    //        int v1 = triangles[i + 1];
    //        int v2 = triangles[i + 2];
    //        AddEdge(adjacency, v0, v1);
    //        AddEdge(adjacency, v1, v2);
    //        AddEdge(adjacency, v2, v0);
    //    }

    //    return adjacency;
    //}

    //private static void AddEdge(Dictionary<int, HashSet<int>> adj, int a, int b)
    //{
    //    if (!adj.ContainsKey(a)) adj[a] = new HashSet<int>();
    //    if (!adj.ContainsKey(b)) adj[b] = new HashSet<int>();
    //    adj[a].Add(b);
    //    adj[b].Add(a);
    //}

    /// <summary>
    /// 중복 위치를 가진 vertex index들을 포함하여 topology neighbor 확장 (depth 제한 적용)
    /// </summary>
    public static HashSet<int> FindTopologyNeighbors(int startIndex, Dictionary<int, HashSet<int>> adjacency, int maxDepth, Mesh mesh, Transform transform)
    {
        Queue<(int, int)> queue = new Queue<(int, int)>();
        HashSet<int> visited = new HashSet<int> { startIndex };
        queue.Enqueue((startIndex, 0));

        while (queue.Count > 0)
        {
            var (current, depth) = queue.Dequeue();
            if (depth >= maxDepth) continue;

            foreach (int neighbor in adjacency[current])
            {
                if (visited.Add(neighbor))
                    queue.Enqueue((neighbor, depth + 1));
            }
        }

        // 중복 위치 정점 보정 포함
        Dictionary<Vector3, List<int>> positionMap = GetOrBuildPositionToIndicesMap(mesh, transform);
        HashSet<int> expanded = new HashSet<int>(visited);
        foreach (int idx in visited)
        {
            Vector3 wp = transform.TransformPoint(mesh.vertices[idx]);
            if (positionMap.TryGetValue(wp, out var dupList))
            {
                foreach (var dupIdx in dupList)
                    expanded.Add(dupIdx);
            }
        }

        expanded.Remove(startIndex);
        return expanded;
    }

    /// <summary>
    /// worldPos에 가장 가까운 정점 index 반환
    /// </summary>
    public static int FindNearestVertexIndex(Vector3 worldPos, Vector3[] worldVertices)
    {
        float minDist = float.MaxValue;
        int nearestIndex = 0;
        for (int i = 0; i < worldVertices.Length; i++)
        {
            float dist = (worldVertices[i] - worldPos).sqrMagnitude;
            if (dist < minDist)
            {
                minDist = dist;
                nearestIndex = i;
            }
        }
        return nearestIndex;
    }

    /// <summary>
    /// 동일 world position을 공유하는 vertex index 리스트 생성
    /// </summary>
    public static Dictionary<Vector3, List<int>> GetOrBuildPositionToIndicesMap(Mesh mesh, Transform transform)
    {
        if (_positionMapCache.TryGetValue(mesh, out var cached))
            return cached;

        var dict = new Dictionary<Vector3, List<int>>();
        Vector3[] verts = mesh.vertices;
        for (int i = 0; i < verts.Length; i++)
        {
            Vector3 worldPos = transform.TransformPoint(verts[i]);
            if (!dict.TryGetValue(worldPos, out var list))
            {
                list = new List<int>();
                dict[worldPos] = list;
            }
            list.Add(i);
        }

        _positionMapCache[mesh] = dict;
        return dict;
    }

    //가시화를 위한 smoothing
    public static Dictionary<Vector3, float> SmoothSaliency(Dictionary<Vector3, float> saliencyMap, Mesh mesh, Transform transform, int depth = 1, float weightSelf = 0.5f)
    {
        var adjacency = SaliencyUtils.GetOrBuildAdjacency(mesh);
        var positionMap = SaliencyUtils.GetOrBuildPositionToIndicesMap(mesh, transform);

        Vector3[] worldPositions = mesh.vertices
            .Select(v => transform.TransformPoint(v)).ToArray();

        Dictionary<Vector3, float> smoothed = new();

        foreach (var kvp in saliencyMap)
        {
            Vector3 center = kvp.Key;
            float centerValue = kvp.Value;

            if (!positionMap.TryGetValue(center, out var indices)) continue;

            HashSet<int> fullNeighbors = new();
            foreach (int idx in indices)
            {
                var neighbors = SaliencyUtils.FindTopologyNeighbors(idx, adjacency, depth, mesh, transform);
                foreach (int n in neighbors)
                    fullNeighbors.Add(n);
            }

            float sum = centerValue * weightSelf;
            float totalWeight = weightSelf;

            foreach (int ni in fullNeighbors)
            {
                Vector3 neighborPos = worldPositions[ni];
                Vector3 rounded = new Vector3(
                    Mathf.Round(neighborPos.x * 1000f) / 1000f,
                    Mathf.Round(neighborPos.y * 1000f) / 1000f,
                    Mathf.Round(neighborPos.z * 1000f) / 1000f
                );

                if (saliencyMap.TryGetValue(rounded, out float neighborVal))
                {
                    sum += neighborVal;
                    totalWeight += 1f;
                }
            }

            float avg = sum / totalWeight;
            smoothed[center] = avg;
        }

        return smoothed;
    }

}
public class PriorityQueue<T>
{
    private readonly SortedDictionary<float, Queue<T>> _dict = new();

    public void Enqueue(T item, float priority)
    {
        if (!_dict.TryGetValue(priority, out var queue))
        {
            queue = new Queue<T>();
            _dict[priority] = queue;
        }
        queue.Enqueue(item);
    }

    public T Dequeue()
    {
        var first = _dict.First();
        var item = first.Value.Dequeue();
        if (first.Value.Count == 0)
            _dict.Remove(first.Key);
        return item;
    }

    public int Count => _dict.Sum(p => p.Value.Count);
}

//public static List<Vector3> FilterVerticesForCalibration(
//    Camera cam, MeshFilter meshFilter, List<Vector3> vertices,
//    float sigma,
//    float viewportEdgeMarginRatio = 0.1f // 메시 화면 투영 기준 가장자리 비율
//)
//{
//    List<Vector3> filtered = new List<Vector3>();
//    Vector3 cameraPos = cam.transform.position;
//    Vector3 cameraRight = cam.transform.right;

//    float minViewDot = 0.2f;
//    //float maxSideViewAmount = 0.25f;

//    int total = vertices.Count;
//    int edgeRemoved = 0, viewDotRemoved = 0, userFacingRemoved = 0;//, sideViewRemoved = 0, accessibilityRemoved = 0;

//    // 중복 제거된 전체 mesh vertex
//    Vector3[] allMeshVertices = GetUniqueWorldVertices(meshFilter);

//    // === [0] Viewport Box Edge Filter ===
//    List<Vector3> vpPositions = vertices
//        .Select(v => cam.WorldToViewportPoint(v))
//        .Where(vp => vp.z > 0f)
//        .ToList();

//    if (vpPositions.Count == 0)
//    {
//        Debug.LogWarning("[Calibration Filter] No visible vertex in Viewport.");
//        return new List<Vector3>();
//    }

//    float vpMinX = vpPositions.Min(vp => vp.x);
//    float vpMaxX = vpPositions.Max(vp => vp.x);
//    float vpMinY = vpPositions.Min(vp => vp.y);
//    float vpMaxY = vpPositions.Max(vp => vp.y);
//    float marginX = (vpMaxX - vpMinX) * viewportEdgeMarginRatio;
//    float marginY = (vpMaxY - vpMinY) * viewportEdgeMarginRatio;

//    List<Vector3> prefiltered = new List<Vector3>();
//    foreach (var v in vertices)
//    {
//        Vector3 vp = cam.WorldToViewportPoint(v);
//        if (vp.z < 0f) continue;

//        bool isEdge =
//            vp.x < vpMinX + marginX || vp.x > vpMaxX - marginX ||
//            vp.y < vpMinY + marginY || vp.y > vpMaxY - marginY;

//        if (isEdge)
//        {
//            edgeRemoved++;
//            //HighlightVertex(v, new Color(1f, 0.4f, 1f), 6f, false); // 핑크색
//            continue;
//        }

//        prefiltered.Add(v);
//    }

//    foreach (Vector3 v in prefiltered)
//    {
//        Vector3 worldNormal = meshFilter.transform.TransformDirection(GetVertexNormal(meshFilter, v)).normalized;
//        Vector3 toCamera = (cameraPos - v).normalized;
//        float dot = Vector3.Dot(worldNormal, toCamera);
//        float sideViewAmount = Mathf.Abs(Vector3.Dot(toCamera, cameraRight));

//        // === [1] ViewDot Filter
//        if (dot < minViewDot)
//        {
//            viewDotRemoved++;
//            Debug.Log($"[ViewDot Removed] Dot={dot:F3}, Pos={v}");

//            continue;
//        }

//        // === [2] UserNormal (시점 무관)
//        Vector3 camSpaceNormal = cam.transform.InverseTransformDirection(worldNormal);
//        float minYNorm = -0.5f;
//        float maxYNorm = 1.0f;
//        float userNormalXMaxAbs = 0.9f;

//        if (camSpaceNormal.y < minYNorm || camSpaceNormal.y > maxYNorm ||
//            Mathf.Abs(camSpaceNormal.x) > userNormalXMaxAbs)
//        {
//            userFacingRemoved++;

//            continue;
//        }

//        // === [3] Accessibility Filter (시점 무관)
//        //float normalVertical = Mathf.Abs(worldNormal.y);
//        //float steepnessThreshold = 0.98f;
//        //float normalToCameraDot = Vector3.Dot(worldNormal, toCamera);

//        //if (normalVertical < (1f - steepnessThreshold)) // y < 0.02
//        //{
//        //    accessibilityRemoved++;

//        //    Debug.Log($"[Accessibility Removed - Steep Normal] y={normalVertical:F2}, Pos={v}");
//        //    continue;
//        //}

//        //if (normalToCameraDot < 0.1f)
//        //{
//        //    accessibilityRemoved++;
//        //    Debug.Log($"[Accessibility Removed - Facing Away] Dot={normalToCameraDot:F2}, Pos={v}");
//        //    continue;
//        //}

//        //List<Vector3> neighbors = GetNeighborsByEuclideanDistance(v, sigma, allMeshVertices);
//        //if (neighbors.Count > 0)
//        //{
//        //    float normalVariance = neighbors.Sum(n =>
//        //    {
//        //        Vector3 neighborNormal = meshFilter.transform.TransformDirection(GetVertexNormal(meshFilter, n)).normalized;
//        //        return (1f - Vector3.Dot(worldNormal, neighborNormal));
//        //    }) / neighbors.Count;

//        //    if (normalVariance > 0.6f)
//        //    {
//        //        accessibilityRemoved++;
//        //        HighlightVertex(v, new Color(1f, 0.4f, 1f), 6f, false);
//        //        Debug.Log($"[Accessibility Removed - High Curvature] Var={normalVariance:F2}, Pos={v}");
//        //        continue;
//        //    }
//        //}

//        // === [4] SideView (주석 상태 유지)
//        /*
//        if (sideViewAmount > maxSideViewAmount)
//        {
//            sideViewRemoved++;
//            continue;
//        }
//        */

//        filtered.Add(v);
//    }

//    Debug.Log($"[Calibration Filter]");
//    Debug.Log($"Total input: {total}");
//    Debug.Log($"Edge removed: {edgeRemoved}");
//    Debug.Log($"ViewDot removed: {viewDotRemoved}");
//    Debug.Log($"UserNormal removed: {userFacingRemoved}");
//    // Debug.Log($"Accessibility removed: {accessibilityRemoved}");
//    // Debug.Log($"SideView removed: {sideViewRemoved}");
//    Debug.Log($"Filtered kept: {filtered.Count}");

//    return filtered;
//}